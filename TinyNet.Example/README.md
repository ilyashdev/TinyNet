# TinyNet.Example

An example application that uses every feature the framework has today: layered
configuration, the three DI lifetimes, a global middleware, route groups with filters,
all five handler interfaces, and reading route, query, header and body values.
Framework-related spots in the code carry comments in English and Russian.

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

| Folder | Contents |
|---|---|
| `Program.cs` | Configuration, services, middleware and the route tree |
| `Controllers/` | `HomeController`, `NotesController`, `NoteController` |
| `Filters/` | `ApiKeyFilter`, `AdminFilter` |
| `Middlewares/` | `RequestIdMiddleware` |
| `Models/` | `Note`, `NoteInput`, `NotePatch` |
| `Services/` | `VisitCounter`, `RequestId`, `NoteStore` |

---

## Routes

| Request | Response |
|---|---|
| `GET /` | HTML with a greeting from configuration, a visit counter and the request id |
| `GET /api/notes?take=N` | The first `N` notes (default 10) |
| `POST /api/notes` | Creates a note from `{"title": ..., "text": ...}`, answers `201` |
| `GET /api/notes/{id}` | One note, or `404` |
| `PUT /api/notes/{id}` | Replaces title and text |
| `PATCH /api/notes/{id}` | Changes only the fields that are sent |
| `DELETE /api/notes/{id}` | Deletes the note, answers `204`; requires `X-Role: admin` |

Everything under `/api` requires the `X-Api-Key` header; the key is `Example:ApiKey` in
`config.json`. A body is read as JSON only with `Content-Type: application/json`.

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

| Feature | Where |
|---|---|
| Configuration layers: default, `config.json`, environment | `AddDefault("Example:Greeting", ...)` in `Program.cs`, `Example:ApiKey` in `config.json`, `TINYNET_` variables |
| Singleton services | `VisitCounter`, `NoteStore` — shared by all requests, so they are thread-safe |
| Scoped service | `RequestId` — one per request; the middleware and `HomeController` get the same instance |
| Transient controllers | Registered by the framework from the routes, one per request |
| Global middleware | `RequestIdMiddleware` adds `X-Request-Id` and `X-Elapsed-Ms` to every response it sees |
| Group filter that stops the request | `ApiKeyFilter` on `/api` answers `401` without calling the controller |
| Filter on one method | `AdminFilter` only on `DELETE /api/notes/{id}`, answers `403` |
| Reading request data | `GetFromRoute<int>("id")`, `GetFromQuery<int>("take") ?? 10`, `GetFromHeader("X-Api-Key")`, `ReadFromBodyAsync<NoteInput>()` |
| Automatic answers | `404` for an unknown path, `405` for a known path with another method, `400` for a malformed JSON body |

```bash
curl -i http://localhost:5768/api/notes/abc -H "$K"                         # 400 from the controller
curl -i -X POST http://localhost:5768/api/notes -H "$K" -H "$J" -d '{broken' # 400 before the pipeline
curl -i -X DELETE http://localhost:5768/api/notes -H "$K"                   # 405
curl -i http://localhost:5768/missing                                       # 404
```

A malformed JSON body is rejected while the request is read, so that `400` carries no
`X-Request-Id`: the middleware never ran. A `400` returned by a controller does carry it.

---

## Static files

`WebRoot` and the `WebRoot:Path` key are kept, but static files are not served at the
moment: the static file handler is being rewritten as a middleware.

---

## Settings

Any key can be overridden through the environment without touching the file, where `__`
stands for `:`:

```bash
TINYNET_Server__Port=8080 TINYNET_Example__ApiKey=secret dotnet run
```