# TinyNet.TestApp

Пример приложения на фреймворке: контроллер, singleton-сервис, конфигурация из файла и
окружения, статические файлы.

[English version](README.md)

---

## Запуск

Рабочий каталог важен — `config.json` и `WebRoot` разрешаются относительно него:

```bash
cd TinyNet.TestApp
dotnet run
```

Приложение слушает `http://localhost:5768`.

---

## Эндпоинты

| Запрос | Ответ |
|---|---|
| `GET /` | HTML со счётчиком из singleton-сервиса |
| `POST /?count=N` | JSON-массив из `N` случайных чисел |

```bash
curl http://localhost:5768/
curl -X POST "http://localhost:5768/?count=5"
```

Счётчик растёт с каждым `GET /` — так видно, что сервис живёт между запросами, а его
контроллер нет.

---

## Статические файлы

В `WebRoot` лежат файлы, которые отдаются по пути:

```bash
curl http://localhost:5768/Index.html
curl http://localhost:5768/Statham.json
curl http://localhost:5768/images.webp --output image.webp
```

---

## Настройки

`config.json` задаёт порт и корень статики. Любой ключ фреймворка переопределяется через
окружение без правки файла, где `__` означает `:`:

```bash
TINYNET_Server__Port=8080 dotnet run
```