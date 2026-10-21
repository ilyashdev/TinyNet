# TinyNet.Example

A small notes web application: one page with a list of notes and a form, plain HTML forms,
no JavaScript. It shows how the framework fits together in a real application — layered
configuration, DI lifetimes, a global middleware, a route group with a filter, synchronous and
asynchronous handlers. Framework-related spots in the code carry comments in English and
Russian.

[Русская версия](README.ru.md)

---

## Running

The working directory matters — `config.json` is resolved relative to it:

```bash
cd TinyNet.Example
dotnet run
```

Open `http://localhost:5768` in a browser.

---

## How it works

| Request                    | What happens                                                                  |
|----------------------------|-------------------------------------------------------------------------------|
| `GET /`                    | The page: the title from configuration, the notes, a form to add one          |
| `POST /notes`              | The add form; redirects back to `/` with `303`, or to `/?error=title`         |
| `POST /notes/{id}/delete`  | The delete button of a note; redirects back to `/` with `303`                 |
| `GET /health`              | `ok`, for a load balancer or a quick check                                     |

After a form is sent the server answers with a redirect, so reloading the page does not send
the form again (Post/Redirect/Get). Notes live in memory and disappear on restart.

---

## Layout

| File                                | Contents                                                        |
|-------------------------------------|-----------------------------------------------------------------|
| `Program.cs`                        | Configuration, services, middleware and the route tree          |
| `Pages/NotesPage.cs`                | The handler class: the page, adding and deleting a note         |
| `Pages/Layout.cs`                   | The HTML skeleton of the page and HTML encoding                 |
| `Pages/Form.cs`                     | Parsing an `application/x-www-form-urlencoded` body             |
| `Filters/SameOriginFilter.cs`       | Rejects forms sent from another site                            |
| `Middlewares/RequestIdMiddleware.cs`| Logs every request with its id and time                         |
| `Services/`                         | `NoteStore` (singleton), `RequestId` (scoped)                    |

---

## What each part shows

| Feature                              | Where                                                                                                   |
|--------------------------------------|---------------------------------------------------------------------------------------------------------|
| Configuration layers                 | `Example:Title`: `AddDefault` in `Program.cs` → `config.json` → `TINYNET_Example__Title`                 |
| Singleton service                    | `NoteStore` — shared by all requests, so it is thread-safe                                              |
| Scoped service                       | `RequestId` — one per request; the middleware and the page footer show the same value                   |
| Handler class                        | `AddGet<NotesPage>("/", h => h.Index)`: registered by the framework from the routes, one per request     |
| Handler without a class              | `AddGet("/health", (request, context) => ...)`: nothing is registered in DI                             |
| Synchronous and asynchronous handlers| `Index` and `Delete` return `HttpResponse`; `Create` reads the body and returns `Task<HttpResponse>`     |
| Group filter that stops the request  | `SameOriginFilter` on `/notes` answers `403` without calling the handler                                |
| Global middleware                    | `RequestIdMiddleware` logs the request id, method, path, status or exception and time                  |
| Reading request data                 | `GetFromQuery("error")`, `GetFromRoute<int>("id")`, `GetFromHeaders("Origin")`, `BufferBodyAsync()`      |
| Building a response                  | HTML through `.Bytes(...)`, a redirect through `.Status(303).AddHeader("Location", ...).Empty()`, `.Text(...)` |
| Automatic answers                    | `400` for a non-numeric `{id}`, `405` for `GET /notes`, `404` for an unknown path                       |

The framework has no form binding and no templates, on purpose: the handler reads the body
and parses the form itself (`Pages/Form.cs`), and the page is an interpolated string. Every
value from the user goes through `Layout.Encode` before it reaches the HTML.

```bash
curl -i -X POST http://localhost:5768/notes/abc/delete                          # 400, not a number
curl -i http://localhost:5768/notes                                             # 405, only POST
curl -i -X POST http://localhost:5768/notes -H "Origin: http://evil.example" -d "title=x"   # 403
```

---

## Settings

Any key can be overridden through the environment without touching the file, where `__`
stands for `:`:

```bash
TINYNET_Server__Port=8080 TINYNET_Example__Title="My notes" dotnet run
```