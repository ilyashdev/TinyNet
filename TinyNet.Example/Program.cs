using TinyNet.Application;
using TinyNet.Example.Filters;
using TinyNet.Example.Middlewares;
using TinyNet.Example.Pages;
using TinyNet.Example.Services;

var builder = new AppBuilder();

// EN: Configuration layers: framework defaults -> AddDefault -> config.json -> TINYNET_ variables.
//     The layer registered last wins; "__" in a variable name stands for ":" (TINYNET_Example__Title).
// RU: Слои конфигурации: дефолты фреймворка -> AddDefault -> config.json -> переменные TINYNET_.
//     Побеждает слой, добавленный последним; "__" в имени переменной означает ":" (TINYNET_Example__Title).
builder
    .AddDefault("Example:Title", "Notes")
    .AddJsonConfig("config.json")
    .AddEnvironmentVariables("TINYNET_");

builder.RegisterMiddleware<RequestIdMiddleware>();

// EN: Singleton — one instance for the whole application, shared by parallel requests.
//     Scoped — one instance per HTTP request.
//     Handler classes and filters are not registered here: the framework registers them from the routes below.
// RU: Singleton — один экземпляр на всё приложение, общий для параллельных запросов.
//     Scoped — один экземпляр на HTTP-запрос.
//     Классы обработчиков и фильтры здесь не регистрируются: фреймворк сам регистрирует их из маршрутов ниже.
builder.Services.AddSingleton<NoteStore>();
builder.Services.AddScoped<RequestId>();

// EN: Routes are the only place where paths are declared. An endpoint is a method + path + handler.
//     "h => h.Index" picks a method of a handler class; the compiler checks that it matches
//     (HttpRequest, HttpContext) -> HttpResponse, or -> Task<HttpResponse> for a method that awaits.
//     The class is created from DI for every request.
// RU: Маршруты — единственное место, где объявляются пути. Эндпоинт — это метод + путь + обработчик.
//     "h => h.Index" выбирает метод класса-обработчика; компилятор проверяет, что он подходит под
//     (HttpRequest, HttpContext) -> HttpResponse или -> Task<HttpResponse> для метода, который чего-то ждёт.
//     Класс создаётся из DI на каждый запрос.
builder.Routes.AddGet<NotesPage>("/", h => h.Index);

// EN: A group adds a path prefix and filters; a group filter applies to every endpoint inside it.
//     Here it guards both forms that change notes.
// RU: Группа добавляет префикс пути и фильтры; фильтр группы действует на все эндпоинты внутри неё.
//     Здесь он защищает обе формы, которые меняют заметки.
builder.Routes
    .AddGroup("/notes")
    .AddFilter<SameOriginFilter>()
    .AddPost<NotesPage>("/", h => h.Create)
    .AddPost<NotesPage>("/{id}/delete", h => h.Delete);

// EN: A handler does not need a class: any delegate with the same signature works. Nothing is registered in DI.
// RU: Обработчику не обязателен класс: подходит любой делегат с той же сигнатурой. В DI ничего не регистрируется.
builder.Routes.AddGet("/health", (request, context) => context.Response().Text("ok"));

// EN: Build checks everything at startup: duplicate routes, a filter applied twice,
//     a singleton that captures a scoped service. Mistakes fail here, not on the first request.
// RU: Build проверяет всё при старте: дубли маршрутов, фильтр, навешанный дважды,
//     синглтон, захвативший scoped-сервис. Ошибки падают здесь, а не на первом запросе.
var app = builder.Build();
await app.Run();