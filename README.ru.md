# TinyNet

HTTP-фреймворк, написанный с нуля на C# под .NET 10.

[English version](README.md)

---

## Состояние

**Работает**

- Текучий построитель приложения
- Внедрение зависимостей с временами жизни singleton, scoped и transient
- Маршрутизация по атрибутам на точных путях
- Глобальные middleware и фильтры на контроллер
- Конфигурация из значений по умолчанию, JSON и переменных окружения
- Чтение запроса с телом фиксированной длины и chunked
- Настраиваемые лимиты запроса с ответами `413`, `408` и `400`
- Контроль приёма: ограниченная очередь и `503` при её заполнении
- Статические файлы

**В работе**

- Keep-alive — сейчас каждый ответ закрывает соединение
- Шаблоны маршрутов вида `/users/{id}`
- Тело запроса и ответа как поток — сейчас тело разбирается как JSON
- Привязка query для типов, кроме чисел
- Статика: кэширование, `ETag`, `304`, `HEAD`

---

## Быстрый старт

```csharp
var builder = new AppBuilder();

builder.AddJsonConfig("config.json");
builder.AddEnvironmentVariables("TINYNET_");

builder.Services.AddSingleton<MyService>();
builder.RegisterMiddleware<LoggingMiddleware>();

var app = builder.Build();
await app.Run();
```

```csharp
[Route("/hello")]
public class HelloController : Controller
{
    private readonly MyService _service;

    public HelloController(MyService service) => _service = service;

    [HttpMethod("GET")]
    public IActionResult Get() => new Ok(new { message = "Hello, World!" });
}
```

```json
{
  "Server": { "Port": 5000 },
  "WebRoot": { "Path": "./WebRoot" }
}
```

---

## Состав решения

| Проект | Назначение |
|---|---|
| `TinyNet` | Фреймворк |
| [`TinyNet.TestApp`](TinyNet.TestApp/README.ru.md) | Пример приложения |
| [`TinyNet.Tests`](TinyNet.Tests/README.ru.md) | Тесты |
| [`TinyNet.K6Bench`](TinyNet.K6Bench/README.ru.md) | Симуляторы нагрузки и сценарии k6 |

---

## Архитектура

```
AppBuilder
    ├── DIContainer
    ├── ConfigurationBuilder
    └── MiddlewarePipeline
            │
            ▼
    WebApplication
            ├── поток приёма "tinynet-accept"
            │       │
            │       ▼
            ├── Channel<NetClient>   — ограничен; заполнен ⇒ 503
            │       │
            │       ▼
            └── пул рабочих          — N = Server:MaxConcurrentRequests
                    │
                    ▼
            MiddlewarePipeline → ControllerHandler → IActionResult
```

Путь запроса:

1. Выделенный поток принимает соединение и кладёт его в ограниченный канал. Когда канал
   заполнен, этот же поток сам отвечает `503` и закрывает соединение.
2. Рабочий забирает соединение и открывает `DIScope` на время запроса.
3. Запрос читается под `HttpLimits`, которые дают `413`, `408` или `400` при превышении
   лимита.
4. Отрабатывает цепочка middleware, затем `ControllerHandler` достаёт контроллер, привязывает
   параметры и вызывает метод.
5. `IActionResult.ExecuteResult` заполняет `HttpResponse`, который пишется в сокет.

---

## Внедрение зависимостей

```csharp
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IMyService, MyService>();
builder.Services.AddTransient<IMyService, MyService>();
builder.Services.AddSingleton<MyService>();
builder.Services.AddInstance<IMyService>(existingInstance);
```

Контроллеры и middleware получают зависимости через конструктор; выбирается конструктор с
наибольшим числом параметров. Циклические зависимости бросают `InvalidOperationException`
при разрешении. Конструкторы компилируются в делегаты при первом обращении и кэшируются,
поэтому разрешение не идёт через рефлексию.

---

## Контроллеры и маршрутизация

Контроллер наследуется от `Controller`, помечается `[Route]` и содержит методы с
`[HttpMethod]`. Маршрут сопоставляется как точный путь, а контроллер обрабатывает по одному
методу на HTTP-глагол.

```csharp
[Route("/products")]
public class ProductsController : Controller
{
    [HttpMethod("GET")]
    public IActionResult GetAll() => new Ok(new[] { "first", "second" });

    [HttpMethod("POST")]
    public IActionResult Create([FromBody] string name) => new Ok(new { created = name });

    [HttpMethod("PUT")]
    public IActionResult Resize([FromQuery] int width) => new Ok(new { width });
}
```

`[FromBody]` берёт свойство JSON-тела с именем параметра. `[FromQuery]` берёт значение из
строки запроса. Принимаются `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD` и `OPTIONS`.
`[NotMapped]` исключает контроллер из маршрутизации.

---

## Middleware и фильтры

```csharp
public class LoggingMiddleware : Middleware
{
    public LoggingMiddleware(RequestDelegate next) : base(next) { }

    public override async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine($"→ {context.Request.Method} {context.Request.Url}");
        await _next(context);
    }
}
```

`RegisterMiddleware<T>()` выполняет middleware для каждого запроса. `RegisterFilter<T>()` —
только для контроллеров, помеченных `[Filter(typeof(T))]`. Сначала идут глобальные
middleware, затем фильтры найденного контроллера, каждая группа в порядке регистрации.
Middleware, не вызывающая `_next`, обрывает цепочку.

---

## Конфигурация

Источники читаются в порядке значения по умолчанию → `config.json` → переменные окружения,
и более поздний источник побеждает. Значения по умолчанию всегда идут первыми, в каком бы
порядке их ни зарегистрировали.

```csharp
builder.AddDefault("Server:Port", "8080");
builder.AddJsonConfig("config.json", optional: false);
builder.AddEnvironmentVariables("TINYNET_");
```

Вложенные ключи JSON разворачиваются через `:`, а `__` в переменной окружения означает тот
же разделитель: `TINYNET_Server__Port=5000` даёт `Server:Port`. Значения читаются через
`IConfiguration`, доступный в любом классе, полученном из DI:

```csharp
var port = configuration.GetValue<int>("Server:Port");
var path = configuration["WebRoot:Path"];
```

### Ключи фреймворка

| Ключ | По умолчанию | Смысл |
|---|---|---|
| `Server:Port` | `5000` | порт прослушивания; `0` — выбирает ОС |
| `Server:MaxConcurrentRequests` | `256` | число рабочих |
| `Server:MaxQueuedConnections` | `1024` | размер очереди; заполнена ⇒ `503` |
| `Server:MaxHeadBytes` | `16384` | лимит головы запроса ⇒ `413` |
| `Server:MaxBodyBytes` | `8388608` | лимит тела запроса ⇒ `413` |
| `Server:ReceiveBufferSize` | `8192` | буфер чтения сокета |
| `Server:ReadTimeoutSeconds` | `15` | время на полный запрос ⇒ `408` |
| `WebRoot:Path` | `./WebRoot` | корень статики, относительный или абсолютный |

---

## Результаты действий

| Класс | Статус | Тело |
|---|---|---|
| `Ok` | 200 | необязательный JSON |
| `BadRequest` | 400 | необязательный JSON |
| `NotFound` | 404 | необязательный JSON |
| `InternalError` | 500 | необязательный JSON |
| `HtmlView` | 200 | HTML |
| `Media` | 200 | текст или байты с явным content type |

```csharp
return new Ok(new { id = 1 });
return new HtmlView("<h1>Hello</h1>");
return new Media(bytes, "image/png");
```

Новый результат наследуется от `BaseResult` для JSON-тела либо от `ActionResult`, чтобы
заполнить ответ самому:

```csharp
public class Created : BaseResult
{
    public Created(object data) : base(201, data) { }
}
```

---

## Статические файлы

URL, содержащий `.`, отдаётся из корня статики, который разрешается относительно рабочего
каталога. Пути, ведущие за пределы корня, отклоняются с `404`, как и отсутствующий файл.

`html`, `css`, `js`, `json`, `xml`, `jpeg`, `jpg`, `png`, `bmp`, `gif`, `tiff`, `tif`,
`webp`, `zip` и `rar` отдаются со своим content type, остальное — как
`application/octet-stream`.

---

## Лицензия

Apache License 2.0 — см. [LICENSE](LICENSE).