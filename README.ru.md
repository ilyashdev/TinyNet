# TinyNet

Лёгкий HTTP веб-фреймворк, вдохновлённый ASP.NET Core, написанный с нуля на C# .NET 10 (LTS).

---

## Содержание

- [Обзор](#обзор)
- [Быстрый старт](#быстрый-старт)
- [Архитектура](#архитектура)
- [Модули](#модули)
  - [Приложение](#приложение)
  - [Внедрение зависимостей](#внедрение-зависимостей)
  - [Контроллеры и маршрутизация](#контроллеры-и-маршрутизация)
  - [Middleware и фильтры](#middleware-и-фильтры)
  - [Конфигурация](#конфигурация)
  - [HTTP](#http)
  - [Action Results](#action-results)
  - [Статические файлы](#статические-файлы)
- [Примеры](#примеры)
- [Лицензия](#лицензия)

---

## Обзор

TinyNet — минималистичный HTTP сервер-фреймворк, реализующий основные концепции современных веб-фреймворков:

- Fluent-builder для сборки приложения
- Внедрение зависимостей с поддержкой Singleton, Scoped и Transient
- Middleware pipeline с поддержкой per-controller фильтров
- Маршрутизация и привязка HTTP-методов через атрибуты
- Многоуровневая система конфигурации через провайдеры, с дефолтами фреймворка
- Асинхронная обработка запросов через ограниченный `Channel<T>` и пул воркеров
- Admission control: выделенный поток приёма и честный `503` под перегрузом
- Настраиваемые лимиты запроса (`413`, `408`, `400`), проверяемые при чтении
- Раздача статических файлов

---

## Быстрый старт

### 1. Создать приложение

```csharp
var builder = new AppBuilder();

builder.AddJsonConfig("config.json");
builder.AddEnvironmentVariables("MYAPP_");

builder.Services.AddSingleton<MyService>();
builder.RegisterMiddleware<LoggingMiddleware>();

var app = builder.Build();
await app.Run();
```

### 2. Создать контроллер

```csharp
[Route("/hello")]
public class HelloController : Controller
{
    private readonly MyService _service;

    public HelloController(MyService service)
    {
        _service = service;
    }

    [HttpMethod("GET")]
    public IActionResult Get()
    {
        return new Ok(new { message = "Привет, мир!" });
    }

    [HttpMethod("POST")]
    public IActionResult Post([FromBody] string name)
    {
        return new Ok(new { message = $"Привет, {name}!" });
    }
}
```

### 3. Файл конфигурации (`config.json`)

```json
{
  "Server": {
    "Port": 5000
  },
  "WebRoot": {
    "Path": "./WebRoot"
  }
}
```

---

## Архитектура

```
AppBuilder
    │
    ├── DIContainer          — регистрация и разрешение зависимостей
    ├── ConfigurationBuilder — провайдеры конфигурации
    └── MiddlewarePipeline   — регистрация middleware
            │
            ▼
    WebApplication
            │
            ├── поток приёма "tinynet-accept"    — выделенный поток ОС
            │       │  NetHandler.Accept()        — блокирующий
            │       ▼
            ├── Channel<NetClient>                — ограниченный; полон ⇒ 503
            │       │
            │       ▼
            └── Пул воркеров (N = Server:MaxConcurrentRequests)
                    │
                    ▼
            ProcessClient
                    ├── DIScope (на время соединения)
                    ├── BuildResponse
                    │       ├── NetClient.GetRequest  — лимиты, таймаут
                    │       └── Dispatch
                    │               └── MiddlewarePipeline.InvokeAsync
                    │                       └── ControllerHandler.Handle
                    │                               ├── Разрешение контроллера через DI
                    │                               ├── Привязка параметров
                    │                               └── Вызов метода
                    └── SendSafely
```

### Жизненный цикл запроса

1. Выделенный поток приёма вызывает блокирующий `NetHandler.Accept()`
2. `NetClient` записывается в ограниченный `Channel<NetClient>`. Если канал полон, этот же
   поток сам отвечает `503` и закрывает соединение
3. Один из N воркеров забирает клиента из канала
4. Создаётся `DIScope` на время жизни соединения
5. `HttpRequest` читается и разбирается под `HttpLimits`, при превышении лимита получается
   `413`, `408` или `400`
6. `MiddlewarePipeline` строит и запускает цепочку middleware
7. `ControllerHandler` разрешает контроллер через DI, привязывает параметры, вызывает метод
8. `IActionResult.ExecuteResult` формирует `HttpResponse`
9. Ответ отправляется по сокету, сокет закрывается

### Почему приём живёт на отдельном потоке

Раньше `AcceptLoop` был задачей в общем пуле потоков. Когда прикладной код блокирует поток
пула — `Thread.Sleep`, `.Result`, любое синхронное ожидание внутри async-обработчика — пул
голодает, продолжение приёма некому выполнить, и сервер перестаёт принимать соединения.
Очередь прослушивания переполняется, и ОС начинает отклонять соединения на уровне TCP:
клиент видит отказ вместо ответа, а в логе не появляется ничего.

Поскольку путь `503` живёт внутри цикла приёма, admission control умирал первым — ровно
тогда, когда был нужен. Приём на собственном потоке, с блокирующим `Accept()` и синхронной
записью `503`, делает этот путь независимым от прикладного кода. Это разделение реактора и
пула у Puma и разделение boss- и worker-групп у Netty, ценой в один поток. Замеры — в
[`TinyNetTestApp/loadtests`](TinyNetTestApp/loadtests/README.ru.md).

Блокирующие обработчики по-прежнему снижают пропускную способность: воркеры работают в пуле.
Выделенный поток гарантирует другое — что сервер деградирует честно и сообщает об этом.

---

## Модули

### Приложение

#### `AppBuilder`

Точка входа для конфигурации и сборки приложения.

```csharp
var builder = new AppBuilder();
```

| Метод | Описание |
|-------|----------|
| `AddJsonConfig(string path)` | Добавляет JSON-файл конфигурации |
| `AddEnvironmentVariables(string? prefix)` | Добавляет переменные окружения как источник конфигурации |
| `RegisterMiddleware<T>()` | Регистрирует глобальный middleware |
| `RegisterFilter<T>()` | Регистрирует условный per-controller фильтр |
| `Services` | Доступ к `DIContainer` для регистрации сервисов |
| `Build()` | Собирает и возвращает `WebApplication` |

#### `WebApplication`

```csharp
await app.Run();               // работает, пока процесс не остановят
await app.Run(cancellation);   // возвращается, когда токен отменён
```

Запускает сервер. Внутри:
- Запускает N воркеров (`Server:MaxConcurrentRequests`), читающих из канала через `ReadAllAsync`
- Поднимает цикл приёма на выделенном фоновом потоке с именем `tinynet-accept`
- При отмене: прекращает слушать, закрывает канал, чтобы воркеры его дочитали, и возвращается

---

### Внедрение зависимостей

`DIContainer` поддерживает три времени жизни сервисов:

| Lifetime | Поведение |
|----------|-----------|
| `Singleton` | Один экземпляр на всё время жизни приложения |
| `Scoped` | Один экземпляр на HTTP-запрос (`DIScope`) |
| `Transient` | Новый экземпляр при каждом запросе |

#### Регистрация

```csharp
// Через интерфейс и реализацию
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IMyService, MyService>();
builder.Services.AddTransient<IMyService, MyService>();

// Только через тип реализации
builder.Services.AddSingleton<MyService>();

// Через объект Type (используется внутри)
builder.Services.AddTransient(typeof(MyService));

// Готовый экземпляр
builder.Services.AddInstance<IMyService>(existingInstance);
```

#### Инъекция через конструктор

Контроллеры и middleware получают зависимости через конструктор автоматически.

```csharp
[Route("/users")]
public class UsersController : Controller
{
    public UsersController(IUserRepository repo, ILogger logger) { ... }
}
```

#### Обнаружение циклических зависимостей

Контейнер обнаруживает циклические зависимости при разрешении и бросает `InvalidOperationException`.

#### Производительность

Вызов конструктора компилируется в нативный делегат через `Expression.Lambda` при первом использовании и кэшируется — последующие разрешения не используют reflection.

---

### Контроллеры и маршрутизация

#### Определение контроллера

Контроллер должен:
- Наследоваться от `Controller`
- Быть помечен атрибутом `[Route]`
- Иметь хотя бы один метод с атрибутом `[HttpMethod]`

```csharp
[Route("/products")]
public class ProductsController : Controller
{
    [HttpMethod("GET")]
    public IActionResult GetAll()
    {
        return new Ok(new[] { "product1", "product2" });
    }

    [HttpMethod("POST")]
    public IActionResult Create([FromBody] string name)
    {
        return new Ok(new { created = name });
    }
}
```

#### Привязка параметров

| Атрибут | Источник | Тип |
|---------|----------|-----|
| `[FromBody]` | JSON тело запроса | Любой JSON-десериализуемый тип |
| `[FromQuery]` | Query-строка URL | `string` |

```csharp
[HttpMethod("GET")]
public IActionResult Search([FromQuery] string term)
{
    return new Ok(new { query = term });
}

[HttpMethod("POST")]
public IActionResult Submit([FromBody] string payload)
{
    return new Ok(payload);
}
```

#### Исключение контроллера из маршрутизации

```csharp
[NotMapped]
public class InternalController : Controller { ... }
```

#### Поддерживаемые HTTP-методы

`GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD`, `OPTIONS`

---

### Middleware и фильтры

#### Создание middleware

Наследуйтесь от `Middleware` и реализуйте `InvokeAsync`:

```csharp
public class LoggingMiddleware : Middleware
{
    public LoggingMiddleware(RequestDelegate next) : base(next) { }

    public override async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine($"→ {context.Request.Method} {context.Request.Url}");
        await _next(context);
        Console.WriteLine($"← {context.Response?.StatusCode}");
    }
}
```

#### Регистрация глобального middleware

Выполняется для каждого запроса:

```csharp
builder.RegisterMiddleware<LoggingMiddleware>();
```

#### Регистрация фильтра

Выполняется только для контроллеров, помеченных `[Filter(typeof(T))]`:

```csharp
builder.RegisterFilter<AuthMiddleware>();
```

```csharp
[Route("/admin")]
[Filter(typeof(AuthMiddleware))]
public class AdminController : Controller { ... }
```

#### Порядок выполнения

Сначала выполняются глобальные middleware, затем фильтры применимые к найденному контроллеру. Все выполняются в порядке регистрации.

---

### Конфигурация

#### Провайдеры

| Провайдер | Регистрация |
|-----------|-------------|
| Дефолты фреймворка | автоматически |
| Собственные дефолты | `builder.AddDefault(key, value)` / `AddDefaults(pairs)` |
| JSON-файл | `builder.AddJsonConfig("config.json", optional: false)` |
| Переменные окружения | `builder.AddEnvironmentVariables("PREFIX_")` |

Провайдеры, добавленные позже, имеют приоритет над ранее добавленными, а дефолты всегда
ставятся первыми независимо от порядка вызовов — поэтому приоритет
`дефолты → json → переменные окружения` нельзя сломать, перепутав последовательность.

#### Дефолты фреймворка

`Application/FrameworkDefaults.cs` — единственное место, где живут собственные дефолты
фреймворка. Всё вынесено в ключи конфигурации, поэтому любой лимит меняется под конкретное
развёртывание без правки кода:

| Ключ | По умолчанию | Значение |
|------|--------------|----------|
| `Server:Port` | `5000` | порт прослушивания |
| `Server:MaxConcurrentRequests` | `256` | число воркеров — потолок конкурентности |
| `Server:MaxQueuedConnections` | `1024` | ёмкость канала; полон ⇒ `503` |
| `Server:MaxHeadBytes` | `16384` | лимит головы запроса ⇒ `413` |
| `Server:MaxBodyBytes` | `8388608` | лимит тела запроса ⇒ `413` |
| `Server:ReceiveBufferSize` | `8192` | буфер чтения из сокета |
| `Server:ReadTimeoutSeconds` | `15` | время на присылку полного запроса ⇒ `408` |
| `WebRoot:Path` | `./WebRoot` | корень статики; относительный или абсолютный |

Ёмкость выводится из первых трёх: потолок пропускной способности равен
`MaxConcurrentRequests / длительность обработчика`, а полная очередь добавляет
`MaxQueuedConnections / MaxConcurrentRequests` секунд ожидания. Оба соотношения проверены
замером — см. [`TinyNetTestApp/loadtests`](TinyNetTestApp/loadtests/README.ru.md).

#### Доступ к конфигурации

`IConfiguration` доступна в любом классе через DI:

```csharp
public class MyService
{
    public MyService(IConfiguration config)
    {
        var port = config["Server:Port"];
        var section = config.GetSection("Server");
        var portFromSection = section["Port"];
    }
}
```

#### Формат ключей

Вложенные JSON-ключи сплющиваются с разделителем `:`:

```json
{ "Server": { "Port": 5000 } }
```
→ `config["Server:Port"]` = `"5000"`

Переменные окружения используют `__` как разделитель, который преобразуется в `:`:

```
PREFIX_Server__Port=5000
```
→ `config["Server:Port"]` = `"5000"`

---

### HTTP

#### `HttpRequest`

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Method` | `string` | HTTP-метод (`GET`, `POST` и т.д.) |
| `Url` | `string` | Путь запроса |
| `Headers` | `Dictionary<string, string>` | Заголовки запроса (регистронезависимые) |
| `Query` | `Dictionary<string, string>` | Параметры query-строки |
| `Body` | `JsonObject?` | Разобранное JSON-тело |

#### `HttpResponse`

| Свойство | Тип | Описание |
|----------|-----|----------|
| `StatusCode` | `int?` | HTTP статус-код |
| `Headers` | `Dictionary<string, string>` | Заголовки ответа |
| `Body` | `string?` | Текстовое тело ответа |
| `BinaryBody` | `byte[]?` | Бинарное тело ответа |

Ответы сериализуются через `ToHttpResponse()` (текст) или `ToHttpResponseBytes()` (бинарный). `Content-Length` устанавливается автоматически.

---

### Action Results

Action results инкапсулируют HTTP-ответ. Все реализуют `IActionResult`.

| Класс | Статус | Описание |
|-------|--------|----------|
| `Ok` | 200 | Успех, опциональное JSON-тело |
| `BadRequest` | 400 | Ошибка клиента, опциональное JSON-тело |
| `NotFound` | 404 | Ресурс не найден |
| `InternalError` | 500 | Внутренняя ошибка сервера |
| `HtmlView` | 200 | HTML-ответ |
| `Media` | 200 | Текстовый или бинарный контент с произвольным Content-Type |

#### Использование

```csharp
return new Ok();                          // 200 пустой
return new Ok(new { id = 1 });           // 200 с JSON-телом
return new BadRequest("Неверный ввод");  // 400 с сообщением
return new NotFound();                   // 404
return new HtmlView("<h1>Привет</h1>");  // 200 text/html
return new Media(bytes, "image/png");    // 200 бинарный
return new Media(text, "text/csv");      // 200 текстовый
```

#### Собственный action result

```csharp
public class Created : BaseResult
{
    public Created(object data) : base(201, data) { }
}
```

---

### Статические файлы

Статические файлы отдаются автоматически для любого URL содержащего `.` (например `/style.css`, `/logo.png`).

Настройте корень статики в `config.json`:

```json
{
  "WebRoot": {
    "Path": "./WebRoot"
  }
}
```

Файлы разрешаются относительно рабочей директории приложения. Если файл не найден — возвращается `404 Not Found`.

#### Поддерживаемые типы контента

`html`, `css`, `js`, `json`, `xml`, `jpeg`, `jpg`, `png`, `bmp`, `gif`, `tiff`, `webp`, `zip`, `rar`

Любое другое расширение отдаётся как `application/octet-stream`.

---

## Примеры

### Полный пример контроллера

```csharp
[Route("/api/items")]
public class ItemsController : Controller
{
    private readonly ItemService _service;

    public ItemsController(ItemService service)
    {
        _service = service;
    }

    [HttpMethod("GET")]
    public IActionResult GetAll()
    {
        var items = _service.GetAll();
        return new Ok(items);
    }

    [HttpMethod("POST")]
    public async Task<IActionResult> Create([FromBody] string name)
    {
        var item = await _service.CreateAsync(name);
        return new Ok(item);
    }
}
```

### Middleware с внедрением зависимостей

```csharp
public class AuthMiddleware : Middleware
{
    private readonly IConfiguration _config;

    public AuthMiddleware(RequestDelegate next, IConfiguration config) : base(next)
    {
        _config = config;
    }

    public override async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var token))
        {
            context.Response = new HttpResponse(401, "Unauthorized");
            return;
        }
        await _next(context);
    }
}
```

### Собственный action result

```csharp
public class NoContent : ActionResult
{
    public NoContent() : base(204) { }
}
```

---

## Лицензия

TinyNet распространяется под лицензией **Apache License 2.0**.

Вы можете свободно использовать, изменять и распространять этот код в личных и коммерческих проектах. Любые изменения должны сохранять оригинальное уведомление об авторских правах. Лицензия также обеспечивает явную защиту от патентных претензий.

Полный текст лицензии — в файле [LICENSE](LICENSE).
