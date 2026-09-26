# TinyNet.Example

Пример приложения на всех возможностях, которые есть у фреймворка сейчас: слоистая
конфигурация, три времени жизни в DI, глобальный middleware, группы маршрутов с фильтрами,
все пять интерфейсов обработчиков и чтение значений пути, query, заголовков и тела.
Места, связанные с фреймворком, прокомментированы в коде на английском и русском.

[English version](README.md)

---

## Запуск

Рабочий каталог важен — `config.json` разрешается относительно него:

```bash
cd TinyNet.Example
dotnet run
```

Приложение слушает `http://localhost:5768`.

---

## Структура

| Папка | Содержимое |
|---|---|
| `Program.cs` | Конфигурация, сервисы, middleware и дерево маршрутов |
| `Controllers/` | `HomeController`, `NotesController`, `NoteController` |
| `Filters/` | `ApiKeyFilter`, `AdminFilter` |
| `Middlewares/` | `RequestIdMiddleware` |
| `Models/` | `Note`, `NoteInput`, `NotePatch` |
| `Services/` | `VisitCounter`, `RequestId`, `NoteStore` |

---

## Маршруты

| Запрос | Ответ |
|---|---|
| `GET /` | HTML с приветствием из конфигурации, счётчиком посещений и id запроса |
| `GET /api/notes?take=N` | Первые `N` заметок (по умолчанию 10) |
| `POST /api/notes` | Создаёт заметку из `{"title": ..., "text": ...}`, отвечает `201` |
| `GET /api/notes/{id}` | Одна заметка или `404` |
| `PUT /api/notes/{id}` | Заменяет заголовок и текст |
| `PATCH /api/notes/{id}` | Меняет только присланные поля |
| `DELETE /api/notes/{id}` | Удаляет заметку, отвечает `204`; нужен `X-Role: admin` |

Всё под `/api` требует заголовок `X-Api-Key`; ключ — `Example:ApiKey` в `config.json`. Тело
читается как JSON только с `Content-Type: application/json`.

```bash
K="X-Api-Key: dev-key"; J="Content-Type: application/json"
curl http://localhost:5768/
curl -X POST http://localhost:5768/api/notes -H "$K" -H "$J" -d '{"title":"first","text":"hello"}'
curl "http://localhost:5768/api/notes?take=5" -H "$K"
curl -X PATCH http://localhost:5768/api/notes/1 -H "$K" -H "$J" -d '{"text":"patched"}'
curl -X DELETE http://localhost:5768/api/notes/1 -H "$K" -H "X-Role: admin"
```

---

## Что показывает каждая часть

| Возможность | Где |
|---|---|
| Слои конфигурации: значение по умолчанию, `config.json`, окружение | `AddDefault("Example:Greeting", ...)` в `Program.cs`, `Example:ApiKey` в `config.json`, переменные `TINYNET_` |
| Singleton-сервисы | `VisitCounter`, `NoteStore` — общие для всех запросов, поэтому потокобезопасны |
| Scoped-сервис | `RequestId` — один на запрос; middleware и `HomeController` получают один экземпляр |
| Transient-контроллеры | Регистрирует фреймворк из маршрутов, по одному на запрос |
| Глобальный middleware | `RequestIdMiddleware` добавляет `X-Request-Id` и `X-Elapsed-Ms` к каждому ответу, который видит |
| Фильтр группы, обрывающий запрос | `ApiKeyFilter` на `/api` отвечает `401`, не вызывая контроллер |
| Фильтр на одном методе | `AdminFilter` только на `DELETE /api/notes/{id}`, отвечает `403` |
| Чтение данных запроса | `GetFromRoute<int>("id")`, `GetFromQuery<int>("take") ?? 10`, `GetFromHeader("X-Api-Key")`, `ReadFromBodyAsync<NoteInput>()` |
| Автоматические ответы | `404` на неизвестный путь, `405` на известный путь с другим методом, `400` на битое JSON-тело |

```bash
curl -i http://localhost:5768/api/notes/abc -H "$K"                         # 400 от контроллера
curl -i -X POST http://localhost:5768/api/notes -H "$K" -H "$J" -d '{broken' # 400 до конвейера
curl -i -X DELETE http://localhost:5768/api/notes -H "$K"                   # 405
curl -i http://localhost:5768/missing                                       # 404
```

Битое JSON-тело отклоняется ещё при чтении запроса, поэтому у такого `400` нет
`X-Request-Id`: middleware не выполнялся. У `400`, который вернул контроллер, он есть.

---

## Статические файлы

`WebRoot` и ключ `WebRoot:Path` оставлены, но статика сейчас не отдаётся: её обработчик
переписывается под middleware.

---

## Настройки

Любой ключ переопределяется через окружение без правки файла, где `__` означает `:`:

```bash
TINYNET_Server__Port=8080 TINYNET_Example__ApiKey=secret dotnet run
```