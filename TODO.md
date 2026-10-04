# TODO: переписывание пайплайна от сокета до контекста

Весь путь запроса от сокета до `HttpContext` переписывается в ветке `dev`. Начато в коммите `18edde1`.

## Состояние на 05.10.2026

1. Старый HTTP удалён целиком: `Http.cs`, `HttpExceptions`, `HttpLimits`, `HttpMethod`, `JsonDefaults`, `RequestValues`, старые `HttpResponse` и `HttpContext`, а также `NetClient` и `NetHandler`. Удаление `ActionResult/` (все результаты и `IActionResult`) подготовлено, но не закоммичено.
2. **Решение не собирается.** На удалённые типы ссылаются:
   1. `Application/AppBuilder.cs`: `NetHandler`, `HttpLimits`, `ReadHttpLimits`, `NotFound` через `ActionResult`;
   2. `Application/WebApplication.cs`: весь цикл приёма и обработки на `NetHandler`, `NetClient`, старом `HttpResponse` и исключениях транспорта;
   3. `Controllers/HttpHandlers.cs`: интерфейсы `I*Handler` возвращают `Task<IActionResult>`;
   4. `Controllers/MediaHandler.cs`;
   5. `Routing/GroupRoute.cs` (`Func<T, HttpContext, Task<IActionResult>>`) и `Routing/RouteDispatcher.cs` (`BaseResult(405)`, `request.Url`, присваивание `request.Route`);
   6. `TinyNet.Example`: все контроллеры и фильтры;
   7. `TinyNet.K6Bench/LoadControllers.cs`;
   8. `TinyNet.Tests`: `HttpParsingTests`, `RequestReadingTests`, `RequestValuesTests`, `StaticFileTests`, `RoutingTests`, `TestControllers`.
3. Новые файлы в `Http/`:
   1. `HttpRequest`: `Method`, `Path`, `Protocol`; `GetFromRoute` и `GetFromQuery` в строковой и типизированной форме (`struct, IParsable<T>`, `InvariantCulture`); `GetFromHeaders`; `GetBodyStream()` с флагом «тело прочитано»; `BufferBodyAsync()`; `ReadJsonAsync<T>()` и перегрузка с options, битый JSON бросает `RequestJsonException`; настройки и токен приходят в `internal` конструктор.
   2. `HttpHeaders`: регистронезависимый, `List<string>` на ключ, проверка CR, LF, NUL в `Add`.
   3. `HttpContext`: `Request`, `Response`, скоуп, `GetService<T>()`. Конструктор публичный.
   4. `RequestJsonException`, `HttpSettings`, `Complete` готовы.
   5. `HttpResponse` пустой.
4. `Transport/`: `Connection`, `IConnectionListener`, `TcpConnectionListener`. Написаны, но не рабочие (раздел 4).
5. `IMiddleware` и `RequestDelegate` пока возвращают `Task`.
6. Папка `Protocols/Http1/` объявлена в `TinyNet.csproj`, файлов в ней нет.

## Сделать

### 1. HttpRequest, доделки
1. **Значения маршрута известны только после роутинга**, а `HttpRequest` создаётся раньше и принимает `route` в конструкторе, `Route` только для чтения. `RouteDispatcher` сейчас присваивает `request.Route`. Решить: `internal` метод для установки значений маршрута из диспетчера или роутинг до создания запроса.
2. Текст ошибки повторного чтения: «Request body has already been read. Call BufferBodyAsync() before the first read to read it more than once.»
3. Хвостовые пробелы в `BufferBodyAsync` и `Parse`.
4. Решить про адрес клиента: `GetIpAddress` удалён, а он нужен для логов и rate limiting. За прокси понадобится `X-Forwarded-For`.
5. Query: парсер на повторе ключа берёт первое значение и не падает.
6. Route: выбрать регистр ключей.

### 2. HttpResponse и Complete
1. Строитель: `Status`, затем `AddHeader`, затем завершающий метод (`Json`, `Text`, `Bytes`, `Empty`), который возвращает `Task<Complete>`. Одно тело, один завершающий вызов.
2. Открыто: строгий порядок вызовов через типы, обязателен ли `Status`, нужны ли короткие формы вроде `NotFound()`.
3. `Json` сам ставит `Content-Type`. Перегрузка с options для симметрии с чтением.
4. Заголовки через `HttpHeaders.Add`, проверка падает в строке вызова. `Set-Cookie` пишется отдельными строками.
5. Открыто: когда ответ уходит в сокет. Либо завершающий метод пишет сам (путь к потоковому ответу и SSE), либо фреймворк пишет после обработчика.

### 3. Сигнатуры на Complete
1. `I*Handler` в `HttpHandlers.cs`: `Task<Complete>` вместо `Task<IActionResult>`.
2. `IMiddleware.InvokeAsync` и `RequestDelegate`: `Task<Complete>`.
3. `GroupRoute`, `EndpointRoute`, `Endpoint`, `MiddlewarePipeline`, `RouteDispatcher` под новые сигнатуры. 404 и 405 отвечать через новый `HttpResponse`.
4. `MediaHandler` переписать под новый ответ (или сразу под статику как middleware).

### 4. Транспорт, баги
1. `TcpConnectionListener` не делает `Bind` и `Listen`, поэтому `LocalEndPoint` равен `null` и конструктор всегда бросает. Параметр `port` не используется.
2. `Accept` передаёт `RemoteEndPoint` слушающего сокета вместо принятого соединения.
3. Только IPv4. Размер очереди `Listen` не зашивать.
4. Цикл приёма не должен крутиться без паузы на повторяющемся `SocketException`.
5. Отступы и хвостовые пробелы в `Connection.cs` и `IConnectionListener.cs`.

### 5. Протокол `Protocols/Http1`
1. Решить, как читать соединение: через `PipeReader` (хвост после головы остаётся в пайпе сам) или через `Stream` с префиксом.
2. Парсер головы: лимиты; 400 на CR и LF в заголовках; 400 на дубли `Content-Length`, `Host`, `Transfer-Encoding` и на `Content-Length` вместе с `Transfer-Encoding`. Повторы остальных заголовков склеиваются через запятую.
3. `RequestBodyStream`: после `Content-Length` байт отдаёт конец потока (иначе чтение тела висит на keep alive), не закрывает соединение в `Dispose`, синхронный `Read` бросает.
4. `ChunkedBodyStream` с тем же внешним видом и подсчётом лимита внутри.
5. `Content-Length` больше `MaxBodyLength` даёт 413 до создания потока. Таймаут чтения через токен с `CancelAfter`, при срабатывании 408.
6. После ответа дочитать непрочитанное тело (`CopyToAsync(Stream.Null)`) или закрыть соединение.
7. Percent декодирование: битое кодирование заменять символом замены. Маршрут сопоставлять по сырому пути, значения декодировать потом (`%2F`).
8. Writer ответа: статус, заголовки, `Content-Length`, `Connection`, таймаут отправки. Заголовки регистронезависимо, без дублей `Connection`.
9. Первый запрос соединения ждёт `KeepAliveTimeout`, а не `ReadTimeout`.

### 6. Ошибки
1. `RequestJsonException` даёт 400 и лог на Debug. Любой другой `JsonException` даёт 500 и лог на Error.
2. Исключения идут через конвейер, middleware их видят. Без перехвата фреймворк отвечает сам. Ошибка при записи заголовков: 500, если они ещё не ушли, иначе закрыть соединение.
3. В лог писать исключение целиком, чтобы не терять вложенное.
4. Заменить лестницу `catch` и `ToErrorResponse` из старого `WebApplication`.

### 7. Сборка и хост
1. Модель конкурентности: задача на соединение с лимитами соединений и запросов в работе (не окончательно).
2. `AppBuilder`: вместо `NetHandler` и `ReadHttpLimits` создавать `TcpConnectionListener` и `HttpSettings`; добавить `builder.Json(...)`, настройки замораживаются на `Build()`.
3. `WebApplication` переписать на `IConnectionListener` и `Protocols/Http1`.
4. `HttpRequest`, `HttpResponse`, `HttpContext` создаются в одной точке, все получают одну замороженную `HttpSettings`.
5. Конструктор `HttpContext` сделать `internal`, `_scope` объявить полем.

### 8. Пространства имён
`TinyNet.Http` путается с `System.Net.Http`. Предложено, не решено: модель в корневом `TinyNet`, протокол в `TinyNet.Protocols.Http1` как `internal`, транспорт в `TinyNet.Transport`.

### 9. Потребители и тесты
1. Перевести `TinyNet.Example` и `TinyNet.K6Bench` на новый ответ.
2. Тесты старого парсера (`HttpParsingTests`, `RequestReadingTests`, `RequestValuesTests`) удалить или переписать под `Protocols/Http1`; `StaticFileTests`, `RoutingTests`, `TestControllers` перевести на новые сигнатуры.
3. Новые тесты: keep alive, pipelining, chunked, 413 и 408, повторное чтение тела.
4. Обновить README.