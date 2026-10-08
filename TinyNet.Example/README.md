# TinyNet.Example

An example application that uses every feature the framework has today: layered
configuration, the three DI lifetimes, a global middleware, route groups with filters,
handler classes and plain delegates, building responses, and reading route, query, header
and body values. Framework-related spots in the code carry comments in English and Russian.

[Русская версия](README.ru.md)

---

## Running

The working directory matters — `config.json` is resolved relative to it:

```bash
cd TinyNet.Example
dotnet run
```

The application listens on `http://localhost:5768`.

---

## Layout

| Folder         | Contents                                               |
|----------------|--------------------------------------------------------|
| `Program.cs`   | Configuration, services, middleware and the route tree |
| `Handlers/`    | `HomeHandler`, `NotesHandler`                          |
| `Filters/`     | `ApiKeyFilter`, `AdminFilter`                          |
| `Middlewares/` | `RequestIdMiddleware`                                  |
| `Models/`      | `Note`, `NoteInput`, `NotePatch`                       |
| `Services/`    | `VisitCounter`, `RequestId`, `NoteStore`               |

---

## Routes

| Request                  | Response                                                                    |
|--------------------------|-----------------------------------------------------------------------------|
| `GET /`                  | HTML with a greeting from configuration, a visit counter and the request id |
| `GET /health`            | `ok`, from a lambda without a handler class                                 |
| `GET /api/notes?take=N`  | The first `N` notes (default 10)                                            |
| `POST /api/notes`        | Creates a note from `{"title": ..., "text": ...}`, answers `201`            |
| `GET /api/notes/{id}`    | One note, or `404`                                                          |
| `PUT /api/notes/{id}`    | Replaces title and text                                                     |
| `PATCH /api/notes/{id}`  | Changes only the fields that are sent                                       |
| `DELETE /api/notes/{id}` | Deletes the note, answers `204`; requires `X-Role: admin`                   |

Everything under `/api` requires the `X-Api-Key` header; the key is `Example:ApiKey` in
`config.json`. The format is chosen by the handler (`ReadJsonAsync`), not by `Content-Type`.

```bash
K="X-Api-Key: dev-key"; J="Content-Type: application/json"
curl http://localhost:5768/
curl -X POST http://localhost:5768/api/notes -H "$K" -H "$J" -d '{"title":"first","text":"hello"}'
curl "http://localhost:5768/api/notes?take=5" -H "$K"
curl -X PATCH http://localhost:5768/api/notes/1 -H "$K" -H "$J" -d '{"text":"patched"}'
curl -X DELETE http://localhost:5768/api/notes/1 -H "$K" -H "X-Role: admin"
```

---

## What each part shows

| Feature                                                   | Where                                                                                                                                              |
|-----------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| Configuration layers: default, `config.json`, environment | `AddDefault("Example:Greeting", ...)` in `Program.cs`, `Example:ApiKey` in `config.json`, `TINYNET_` variables                                     |
| Singleton services                                        | `VisitCounter`, `NoteStore` — shared by all requests, so they are thread-safe                                                                      |
| Scoped service                                            | `RequestId` — one per request; the middleware and `HomeHandler` get the same instance                                                              |
| Handler classes                                           | `AddGet<HomeHandler>("/", h => h.Index)`: registered by the framework from the routes, one per request                                             |
| Handler without a class                                   | `AddGet("/health", (request, context) => ...)`: nothing is registered in DI                                                                        |
| Global middleware                                         | `RequestIdMiddleware` logs the request id, method, path, status or exception and time                                                              |
| Group filter that stops the request                       | `ApiKeyFilter` on `/api` answers `401` without calling the handler                                                                                 |
| Filter on one method                                      | `AdminFilter` only on `DELETE /api/notes/{id}`, answers `403`                                                                                      |
| Reading request data                                      | `GetFromRoute<int>("id")`, `GetFromQuery<int>("take") ?? 10`, `GetFromHeaders("X-Api-Key")`, `ReadJsonAsync<NoteInput>()`                          |
| Building a response                                       | `context.Response().Status(...).Json(...)`, `.Text(...)`, `.Bytes(...)`, `.Empty()`                                                                |
| Automatic answers                                         | `404` for an unknown path, `405` with `Allow` for a known path with another method, `400` for a malformed JSON body or a value that does not parse |

```bash
curl -i "http://localhost:5768/api/notes?take=many" -H "$K"                # 400, not a number
curl -i http://localhost:5768/api/notes/abc -H "$K"                         # 400, not a number
curl -i -X POST http://localhost:5768/api/notes -H "$K" -H "$J" -d '{}'     # 400 from the handler
curl -i -X POST http://localhost:5768/api/notes -H "$K" -H "$J" -d '{broken' # 400, not JSON
curl -i -X DELETE http://localhost:5768/api/notes -H "$K"                   # 405
curl -i http://localhost:5768/missing                                       # 404
```

A value that does not parse and a malformed JSON body throw while the handler reads them.
The exception travels back through the filters and the middleware, and the framework answers
`400` with the reason. `RequestIdMiddleware` sees it on the way out and logs the exception
name instead of a status.

---

## Static files

The `WebRoot` folder is kept, but static files are not served at the moment: the static
file handler is being rewritten as a middleware.

---

## Settings

Any key can be overridden through the environment without touching the file, where `__`
stands for `:`:

```bash
TINYNET_Server__Port=8080 TINYNET_Example__ApiKey=secret dotnet run
```