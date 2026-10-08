# TODO

Пайплайн от сокета до контекста переписан в ветке `dev` (начато в `18edde1`). Решение собирается, 37 тестов зелёные.

## Состояние на 08.10.2026

Сделано по старому TODO от 05.10:

1. **Модель.** `HttpRequest` (маршрут ставит диспетчер через `SetRoute`, ключи маршрута без учёта регистра, на повторе
   ключа query берётся первое значение), `ResponseBuilder` → `HttpResponse` (`Json`, `Text`, `Bytes`, `Stream`, `Empty`),
   `HttpContext` с `internal` конструктором. Битое значение маршрута или query бросает `RequestValueException` (400),
   битый JSON — `RequestJsonException` (400).
2. **Сигнатуры.** `RequestDelegate` и `IMiddleware` возвращают `Task<HttpResponse>`. Контроллеры с `I*Handler` и
   `ActionResult` удалены; маршрут — `AddGet(path, delegate)` или `AddGet<T>(path, h => h.Method)`.
3. **Транспорт.** `TcpConnectionListener` с `Bind`/`Listen(backlog)`, верный `RemoteEndPoint`, пауза с джиттером на
   повторяющемся `SocketException`.
4. **Протокол `Protocols/Http1`.** `PipeReader`, строгий парсер головы по RFC 9112, `FixedBodyStream` и
   `ChunkedBodyStream`, 413 и 408 на тело, дочитывание непрочитанного тела, percent-декодирование после разбиения пути,
   writer ответа с chunked для потока без длины, `100-continue`, первый запрос ждёт `KeepAliveTimeoutSeconds`.
5. **Сборка и хост.** `AppBuilder.Json(...)`, единая замороженная `HttpSettings`, `WebApplication` на
   `IConnectionListener` и `IHttpProtocol`.
6. **Конкурентность.** Задача на соединение, слот на запрос (`LimitConcurrency`: `MaxConcurrentRequests`,
   `MaxQueuedRequests`, `RequestQueueTimeoutSeconds`), `MaxConnections`, `AcceptLoops`, `ListenBacklog`, `Task.Run` при
   приёме. Замеры в `TinyNet.K6Bench/README.ru.md`.
7. **Потребители.** `TinyNet.Example`, `TinyNet.K6Bench`, тесты и README переведены на новый API.

## Сделать

### 1. Граница протокола

1. Политику ошибок (`ConnectionHandler.ToErrorResponse`) вынести из протокола в обёртку над `RequestDelegate` рядом с
   `LimitConcurrency`. Протокол отвечает сам только на ошибки разбора и чтения тела.
2. Создание `DIScope` на запрос тоже вынести в обёртку: протоколу достаточно вызвать приложение.
3. `LimitConcurrency` и `NotFound` переложить из `AppBuilder` в отдельное место (обёртки фреймворка).

### 2. Ответ

1. `HttpResponse.Headers` открыт и позволяет добавить заголовок в обход проверок `ResponseBuilder.AddHeader`. Сделать
   `Headers` снова `internal` и дать `HttpResponse.AddHeader` с той же проверкой, чтобы middleware могли дописать
   заголовки после `next`.
2. `ResponseBuilder` отдаёт свой `_headers` в каждый ответ: два ответа из одного строителя делят заголовки. Копировать.

### 3. Публичная граница

1. `MiddlewarePipeline`, `UrlRouter`, `Endpoint` публичны, их можно собрать в обход `Build()`. Сделать `internal`.
2. `IConnectionListener`, `IHttpProtocol`, `Connection`, `TcpConnectionListener` публичны, но `AppBuilder` всегда создаёт
   свои. Либо `internal`, либо точка подключения (`builder.UseListener(...)`).
3. `AppBuilder.Services { get; init; }` позволяет подменить контейнер. Оставить только `get`.
4. `DIContainer.IsService` — проверить, нужен ли снаружи.

### 4. Хост

1. Таймаут отправки: клиент, который не читает ответ, держит соединение без ограничения.
2. Предел времени на мягкую остановку: `Run` ждёт все соединения без таймаута.
3. Адрес клиента для логов и rate limiting; за прокси — `X-Forwarded-For`.
4. Абстракция логирования вместо `Console.WriteLine`. Исключение писать целиком.

### 5. DI

1. `Validate()` не видит незарегистрированные зависимости — падают только при разрешении.
2. Синглтон, конструктор которого бросил, сломан навсегда (`Lazy` кэширует исключение).

### 6. Порядок в именах

1. `Protocols/ServerLimits.cs` и `Protocols/RequestHead.cs` лежат в корне `Protocols`, а пространство имён у них `Http1`.
   Перенести в `Protocols/Http1`.
2. `TinyNet.Http` путается с `System.Net.Http`. Предложено, не решено: модель в корневом `TinyNet`, протокол
   `internal` в `TinyNet.Protocols.Http1`.
3. Исключения разложены по `HandlerExceptions` и `ProtocolExceptions`, а пространство имён одно.

### 7. Возможности

1. Статические файлы как middleware (проверка выхода за web root уже была, тесты удалены вместе со старым хендлером).
2. Зафиксировать принятые решения, которые разошлись с прежними: маршрут через делегат вместо `I*Handler`,
   `GetFromRoute<T>` бросает 400 на мусор вместо `null`.

### 8. Бенчмарки

1. Добавить в `TinyNet.K6Bench/README` прогон 300k rps и замер с k6 и сервером на разных ядрах (потолок k6 ≈124k rps на
   этой машине, сервер ≈78k rps на физическое ядро).
