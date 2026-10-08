# TinyNet

Веб-фреймворк для .NET 10 без лишнего, написанный с нуля вокруг одной идеи — **явного
управления**. Всё, что происходит с запросом, написано в вашем коде, а не решается где-то вне
поля зрения соглашениями, привязкой или сканированием.

[English version](README.md)

---

## Зачем

Большие фреймворки многое делают за вас, и многое из этого происходит там, где этого не
видно: привязка моделей подбирает значения, соглашения выбирают обработчики, сканирование
подхватывает классы. Несколько способов настроить одно и то же влияют друг на друга. Когда
результат неверен, отлаживаешь фреймворк, а не своё приложение.

TinyNet идёт другим путём:

- **Явный поток управления.** Запрос проходит через дерево маршрутов, фильтры, которые вы
  навесили, и обработчик, который вы написали. Обработчик сам читает нужные значения из
  `HttpRequest` и возвращает `HttpResponse`, который сам собрал.
- **Один способ на каждую задачу.** Маршруты объявляются в одном дереве в `Program.cs`,
  данные запроса читаются из одного места, ответ собирается одним способом. Два способа
  сделать одно и то же считаются ошибкой.
- **Ничего лишнего.** Свой HTTP/1.1-сервер, DI-контейнер, конфигурация и конвейер примерно в
  3 300 строк, без NuGet-зависимостей и без ASP.NET под капотом.
- **Фреймворк решает то, что не стоит выбора.** Времена жизни классов-обработчиков, фильтров и
  middleware фиксированы, как и модель конкурентности.
- **Проверка до первого запроса.** Компилятор проверяет, что маршрут указывает на метод с
  правильной сигнатурой; `Build()` проверяет маршруты, фильтры и времена жизни сервисов до
  того, как сервер примет первое соединение.
- **Видимая перегрузка.** Запросы сверх лимита параллельности ждут в ограниченной очереди;
  когда она заполнена, клиент получает `503`, а не зависшее или оборванное соединение.

---

## Состояние

**Работает**

- Текучий построитель приложения
- Внедрение зависимостей с временами жизни singleton, scoped и transient
- Проверка времён жизни на старте: scoped внутри singleton роняет сборку приложения
- Явное дерево маршрутов: группы, вложенные группы и шаблоны вида `/users/{id}`
- Обработчики — делегаты или методы обычных классов, создаваемых из DI на каждый запрос
- Глобальные middleware, фильтры на группах маршрутов и на отдельных эндпоинтах
- `404` на неизвестный путь, `405` с `Allow` на известный путь с другим методом, `HEAD`
  обслуживается эндпоинтом `GET`
- Чтение значений пути, query, заголовков и тела (поток, буфер или JSON) из `HttpRequest`
- Строитель ответа: статус, заголовки и тело — JSON, текст, байты, поток или пустое
- Конфигурация из значений по умолчанию, JSON и переменных окружения
- HTTP/1.1 и HTTP/1.0 со строгим разбором по RFC 9112; keep-alive и пайплайнинг запросов
- Тела запроса фиксированной длины и chunked, `Expect: 100-continue`
- Лимиты запроса с ответами `400`, `408`, `413` и `431`
- Конкурентность: слот на запрос, ограниченная очередь и `503` при её заполнении; лимит
  соединений

**Пока нет**

- Статические файлы
- Адрес клиента (`RemoteEndPoint`, `X-Forwarded-For`)
- Абстракция логирования: фреймворк пишет в `Console`
- Таймаут отправки и предел времени на мягкую остановку
- TLS, HTTP/2

---

## Быстрый старт

```csharp
var builder = new AppBuilder();

builder.AddJsonConfig("config.json");
builder.AddEnvironmentVariables("TINYNET_");

builder.Services.AddSingleton<Greeter>();
builder.RegisterMiddleware<LoggingMiddleware>();

builder.Routes.AddGet("/health", (request, context) =>
    Task.FromResult(context.Response().Text("ok")));

builder.Routes
    .AddGroup("/hello")
    .AddGet<HelloHandler>("/{name}", h => h.Get);

var app = builder.Build();
await app.Run();
```

```csharp
public class HelloHandler
{
    private readonly Greeter _greeter;

    public HelloHandler(Greeter greeter) => _greeter = greeter;

    public Task<HttpResponse> Get(HttpRequest request, HttpContext context)
        => Task.FromResult(context.Response().Json(new { message = _greeter.Greet(request.GetFromRoute("name")) }));
}
```

```json
{
  "Server": { "Port": 5000 }
}
```

Полное приложение, использующее все возможности, — в [`TinyNet.Example`](TinyNet.Example/README.ru.md).

---

## Состав решения

| Проект                                            | Назначение                                           |
|---------------------------------------------------|------------------------------------------------------|
| `TinyNet`                                         | Фреймворк                                            |
| [`TinyNet.Example`](TinyNet.Example/README.ru.md) | Пример приложения, использующего все возможности     |
| [`TinyNet.Tests`](TinyNet.Tests/README.ru.md)     | Тесты                                                |
| [`TinyNet.K6Bench`](TinyNet.K6Bench/README.ru.md) | Симуляторы нагрузки и сценарии k6                    |

---

## Архитектура

```
AppBuilder
    ├── DIContainer
    ├── ConfigurationBuilder
    ├── Routes (GroupRoute)
    └── MiddlewarePipeline
            │
            ▼
    WebApplication
            ├── циклы приёма         — Server:AcceptLoops; сверх Server:MaxConnections ⇒ 503, соединение закрыто
            │       │
            │       ▼
            ├── задача на соединение (Protocols/Http1)
            │       │
            │       ▼
            ├── лимит запросов       — Server:MaxConcurrentRequests в работе,
            │                          Server:MaxQueuedRequests в ожидании ⇒ иначе 503
            │       │
            │       ▼
            └── глобальные middleware → роутер → фильтры групп и эндпоинта → обработчик → HttpResponse
```

| Папка         | Что внутри                                                              | Зависит от                  |
|---------------|-------------------------------------------------------------------------|-----------------------------|
| `Transport`   | сокеты: `IConnectionListener`, `TcpConnectionListener`, `Connection`    | —                           |
| `Http`        | модель: `HttpRequest`, `ResponseBuilder`, `HttpResponse`, `HttpContext` | `DI`, `Exceptions`          |
| `Protocols`   | парсер HTTP/1.1, потоки тела, запись ответа                             | `Http`, `Transport`, `DI`   |
| `Middlewares` | `IMiddleware`, конвейер                                                 | `Http`, `DI`                |
| `Routing`     | дерево маршрутов, роутер, диспетчер                                     | `Http`, `Middlewares`, `DI` |
| `Application` | `AppBuilder` связывает всё вместе, `WebApplication` запускает           | всё                         |

Модель ничего не знает о сокетах, транспорт ничего не знает об HTTP.

`AppBuilder.Build()` делает всю работу заранее: обходит дерево маршрутов, регистрирует
классы-обработчики и фильтры в DI, строит роутер, проверяет времена жизни и собирает все
цепочки фильтров. Ошибка в любом из этих мест роняет сборку, а не первый запрос.

Путь запроса:

1. Один из `Server:AcceptLoops` циклов приёма забирает соединение из очереди ОС размером
   `Server:ListenBacklog`. Если открыто больше `Server:MaxConnections` соединений, он отвечает
   `503` и закрывает соединение.
2. Соединение получает свою задачу, которая читает из него запросы один за другим и открывает
   свежий `DIScope` на каждый.
3. Голова разбирается строго по RFC 9112 под лимитами головы и тела, которые дают `400`,
   `408`, `413` или `431`. Байты, прочитанные за границей запроса, остаются в буфере для
   следующего.
4. Запрос занимает один из `Server:MaxConcurrentRequests` слотов. Если все заняты, он ждёт в
   очереди до `Server:MaxQueuedRequests` запросов не дольше `Server:RequestQueueTimeoutSeconds`;
   сверх этого ответ — `503` с `Connection: close`.
5. Отрабатывают глобальные middleware, затем роутер ищет эндпоинт по пути и методу: `404`,
   если путь неизвестен, `405` с `Allow`, если путь есть, а метода на нём нет.
6. Фильтры групп эндпоинта выполняются от внешней к внутренней, затем фильтры самого
   эндпоинта, затем обработчик. Слот освобождается, как только обработчик вернул
   `HttpResponse`, ещё до записи ответа в сокет.
7. Соединение переиспользуется, пока клиент не попросит закрыть, пока не исчерпается
   `Server:KeepAliveMax` или пока оно не простоит `Server:KeepAliveTimeoutSeconds` между
   запросами.

Простаивающее keep-alive соединение слот не держит: оно только ждёт байтов.
`MaxConcurrentRequests` ограничивает работу, которая идёт, `MaxConnections` — память и сокеты.

---

## Внедрение зависимостей

```csharp
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IMyService, MyService>();
builder.Services.AddTransient<IMyService, MyService>();
builder.Services.AddSingleton<MyService>();
builder.Services.AddInstance<IMyService>(existingInstance);
```

Зависимости приходят через конструктор; выбирается конструктор с наибольшим числом
параметров. Конструкторы компилируются в делегаты при первом обращении и кэшируются.
Циклические зависимости бросают `InvalidOperationException` при разрешении.

Времена жизни частей фреймворка задаёт сам фреймворк, а не пользователь:

| Часть             | Время жизни               | Кто регистрирует               |
|-------------------|---------------------------|--------------------------------|
| Класс-обработчик  | transient, один на запрос | фреймворк, из маршрутов        |
| Фильтр            | singleton                 | фреймворк, из `AddFilter<T>()` |
| Middleware        | singleton                 | `RegisterMiddleware<T>()`      |

Middleware и фильтры общие для параллельных запросов, поэтому должны быть потокобезопасными и
брать в конструктор только синглтоны. Сервисы запроса берутся внутри `InvokeAsync` через
`context.GetService<T>()`.

`AppBuilder.Build()` проверяет времена жизни до старта сервера. Синглтон, зависящий от
scoped-сервиса напрямую или через transient, захватил бы его на всё время жизни процесса,
поэтому сборка падает с указанием всей цепочки:

```
Captive dependency: Reports (Singleton) -> ReportBuilder (Transient) -> DbSession (Scoped)
```

Зависимость незарегистрированного типа проверка пропускает — она по-прежнему бросает при
разрешении.

---

## Маршруты и обработчики

Маршруты объявляются в одном месте — в дереве `builder.Routes`. У всех обработчиков одна
сигнатура:

```csharp
delegate Task<HttpResponse> RequestDelegate(HttpRequest request, HttpContext context);
```

Обработчик — либо делегат, либо метод класса, который фреймворк создаёт из DI на каждый
запрос. `h => h.Get` выбирает метод, а сигнатуру проверяет компилятор:

```csharp
builder.Routes.AddGet("/health", (request, context) =>
    Task.FromResult(context.Response().Text("ok")));

var users = builder.Routes
    .AddGroup("/users")
    .AddFilter<AuthFilter>()
    .AddGet<UsersHandler>("/", h => h.List)
    .AddPost<UsersHandler>("/", h => h.Create, endpoint => endpoint.AddFilter<AdminFilter>())
    .AddGet<UsersHandler>("/{id}", h => h.Get)
    .AddDelete<UsersHandler>("/{id}", h => h.Delete, endpoint => endpoint.AddFilter<AdminFilter>());
```

`AddGet`, `AddPost`, `AddPut`, `AddPatch` и `AddDelete` возвращают группу, поэтому вызовы идут
цепочкой; `AddGroup` возвращает новую группу.

- Фильтр группы действует на все эндпоинты внутри неё, включая вложенные группы. Фильтры
  выполняются от внешней группы к внутренней, затем фильтры самого эндпоинта; внутри уровня —
  в порядке добавления. Фильтр, добавленный в группу после её эндпоинтов, тоже действует.
- Группу можно собрать отдельно и подключить через `AddGroup(group)`; у группы один родитель.
- Запрос `HEAD` обслуживает эндпоинт `GET` того же пути, тело не отправляется.
- Сопоставление идёт по сегментам. Точный сегмент предпочитается параметру, тупиковая ветка
  откатывается, поэтому `/users/me` выигрывает у `/users/{id}`, а `POST /users/me` всё равно
  дойдёт до `POST /users/{id}`, если на `/users/me` есть только `GET`. Регистр сегментов не
  учитывается, хвостовой слэш игнорируется, а каждый сегмент декодируется из `%XX` уже после
  разбиения — так `%2F` не подсунет разделитель.
- `Build()` падает на повторе метода и пути, фильтре, навешанном дважды на пути к эндпоинту,
  двух разных именах параметра на одной позиции, смешанном сегменте вида `v{id}` и группе,
  подключённой ко второму родителю. После `Build()` дерево маршрутов менять нельзя.

---

## Чтение данных запроса

Обработчик читает всё из `HttpRequest`. Ничего не привязывается к параметрам метода:

```csharp
var id = request.GetFromRoute<int>("id");            // int?, 400, если не число
var take = request.GetFromQuery<int>("take") ?? 10;  // int?, null, если ключа нет
var key = request.GetFromHeaders("X-Api-Key");       // string?

var input = await request.ReadJsonAsync<NoteInput>(); // NoteInput?, 400, если не JSON
if (input is null)
    return context.Response().Status(StatusCodes.BadRequest).Text("Body must be a note");

var service = context.GetService<MyScopedService>();
```

- `GetFromRoute` и `GetFromQuery` без аргумента типа возвращают `string?`, а с ним — `T?` для
  любого значимого типа с `IParsable<T>`. `null` значит, что значения нет. Значение, которое
  есть, но не разбирается, бросает `RequestValueException`, ответ — `400`. Значения
  разбираются в инвариантной культуре.
- `ReadJsonAsync<T>()` читает тело с настройками из `builder.Json(...)` (по умолчанию
  `JsonSerializerDefaults.Web`: имена без учёта регистра, в ответах camelCase). Тело, которое
  не является JSON, бросает `RequestJsonException`, ответ — `400`.
- `GetBodyStream()` отдаёт тело потоком, который читается один раз. `BufferBodyAsync()` читает
  его в память, после чего тело можно читать сколько угодно раз.
- Слишком большое или слишком медленное тело обрывает чтение с `413` или `408` и закрывает
  соединение. Тело, которое обработчик не прочитал, дочитывается перед следующим запросом.

---

## Сборка ответа

`context.Response()` возвращает строитель. Сначала статус и заголовки, затем ровно один метод,
который задаёт тело и возвращает `HttpResponse`:

```csharp
return context.Response().Json(note);
return context.Response().Status(StatusCodes.Created).Json(note);
return context.Response().Status(StatusCodes.NoContent).Empty();
return context.Response().AddHeader("Cache-Control", "no-store").Text("ok");
return context.Response().Bytes(html, "text/html; charset=utf-8");
return context.Response().Stream(async (stream, ct) => await file.CopyToAsync(stream, ct), "application/pdf");
```

Статус по умолчанию — `200`. `Content-Length`, `Transfer-Encoding` и `Keep-Alive` ставит
сервер, добавить их нельзя; `Connection` принимает только `close`, после ответа соединение
закрывается. Поток без длины клиентам HTTP/1.1 отправляется chunked.

---

## Middleware и фильтры

Middleware и фильтры реализуют один и тот же интерфейс:

```csharp
public class LoggingMiddleware : IMiddleware
{
    public async Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context, RequestDelegate next)
    {
        var response = await next(request, context);
        Console.WriteLine($"{request.Method} {request.Path} -> {response.StatusCode}");
        return response;
    }
}
```

`RegisterMiddleware<T>()` выполняет его на каждый запрос, до роутинга, в порядке регистрации.
`AddFilter<T>()` на группе или эндпоинте выполняет его только для этой части дерева
маршрутов. Middleware или фильтр, вернувшие ответ без вызова `next`, обрывают запрос:

```csharp
if (request.GetFromHeaders("X-Api-Key") != _apiKey)
    return Task.FromResult(context.Response().Status(StatusCodes.Unauthorized).Text("Missing X-Api-Key"));
return next(request, context);
```

---

## Ошибки

Исключение из обработчика проходит обратно через фильтры и middleware, поэтому они могут его
поймать, записать в лог или превратить в другой ответ. На исключение, которое никто не
поймал, отвечает фреймворк:

| Исключение                                      | Ответ             | Соединение |
|-------------------------------------------------|-------------------|------------|
| `RequestValueException`, `RequestJsonException` | `400`             | остаётся   |
| тело слишком большое или битое                  | `413`, `400`      | закрыто    |
| тело слишком медленное                          | `408`             | закрыто    |
| любое другое                                    | `500`, пишется в лог | остаётся |

Ошибки в голове запроса (`400`, `408`, `431` и `413` на заявленный `Content-Length` сверх
лимита) получают ответ до конвейера, поэтому middleware их не видят.

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
var key = configuration["Example:ApiKey"];
```

### Ключи фреймворка

| Ключ                                | По умолчанию | Смысл                                                                  |
|-------------------------------------|--------------|------------------------------------------------------------------------|
| `Server:Port`                       | `5000`       | порт прослушивания; `0` — выбирает ОС                                  |
| `Server:ListenBacklog`              | `512`        | очередь ОС для ещё не принятых соединений; ОС может её урезать         |
| `Server:AcceptLoops`                | `1`          | параллельных циклов приёма, не меньше `1`                              |
| `Server:MaxConnections`             | `10000`      | открытых соединений; сверх этого новое соединение получает `503`       |
| `Server:MaxConcurrentRequests`      | `256`        | запросов в обработке одновременно                                      |
| `Server:MaxQueuedRequests`          | `1024`       | запросов, ждущих слот; очередь заполнена ⇒ `503`                       |
| `Server:RequestQueueTimeoutSeconds` | `2`          | сколько запрос ждёт слот ⇒ `503`; саму обработку не ограничивает       |
| `Server:MaxHeadBytes`               | `32768`      | лимит головы запроса ⇒ `431`                                           |
| `Server:MaxBodyBytes`               | `1048576`    | лимит тела запроса ⇒ `413`                                             |
| `Server:HeadersTimeoutSeconds`      | `10`         | время на голову после её первого байта ⇒ `408`                         |
| `Server:BodyGracePeriodSeconds`     | `5`          | сколько тело может идти до проверки минимальной скорости               |
| `Server:MinBodyBytesPerSecond`      | `240`        | минимальная скорость загрузки тела ⇒ `408`                             |
| `Server:KeepAliveMax`               | `1000`       | сколько запросов обслуживается на одном соединении                     |
| `Server:KeepAliveTimeoutSeconds`    | `60`         | сколько секунд соединение может простаивать до следующего запроса      |

`KeepAliveTimeoutSeconds` ограничивает паузу **между** запросами, а не жизнь соединения.
Простаивающее соединение стоит только памяти, поэтому таймаут может быть длинным. За обратным
прокси с пулом апстрим-соединений он должен оставаться **выше** таймаута простоя у прокси —
иначе сервер закроет соединение из пула, а клиент получит `502`.

Лимит тела важен для памяти: `ReadJsonAsync` и `BufferBodyAsync` держат тело целиком, поэтому
худший случай — `MaxBodyBytes × MaxConcurrentRequests`. Поднимать его стоит для эндпоинта,
который читает тело потоком через `GetBodyStream()`, а не для JSON.

---

## Лицензия

Apache License 2.0 — см. [LICENSE](LICENSE).
