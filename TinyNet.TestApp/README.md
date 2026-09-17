# TinyNet.TestApp

A sample application showing the framework in use: a controller, a singleton service,
configuration from a file and environment, and static files.

[Русская версия](README.ru.md)

---

## Running

The working directory matters — `config.json` and `WebRoot` are resolved relative to it:

```bash
cd TinyNet.TestApp
dotnet run
```

The application listens on `http://localhost:5768`.

---

## Endpoints

| Request | Response |
|---|---|
| `GET /` | HTML with a counter held by the singleton service |
| `POST /?count=N` | JSON array of `N` random numbers |

```bash
curl http://localhost:5768/
curl -X POST "http://localhost:5768/?count=5"
```

The counter grows with every `GET /`, which shows that the service survives between
requests while its controller does not.

---

## Static files

`WebRoot` holds the files served by path:

```bash
curl http://localhost:5768/Index.html
curl http://localhost:5768/Statham.json
curl http://localhost:5768/images.webp --output image.webp
```

---

## Settings

`config.json` sets the port and the static root. Any framework key can be overridden
through the environment without touching the file, where `__` stands for `:`:

```bash
TINYNET_Server__Port=8080 dotnet run
```