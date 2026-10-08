using TinyNet.Application;
using TinyNet.Example.Filters;
using TinyNet.Example.Handlers;
using TinyNet.Example.Middlewares;
using TinyNet.Example.Services;

var builder = new AppBuilder();

// EN: Configuration layers: framework defaults -> AddDefault -> config.json -> TINYNET_ variables.
//     The layer registered last wins; "__" in a variable name stands for ":" (TINYNET_Example__ApiKey).
// RU: Слои конфигурации: дефолты фреймворка -> AddDefault -> config.json -> переменные TINYNET_.
//     Побеждает слой, добавленный последним; "__" в имени переменной означает ":" (TINYNET_Example__ApiKey).
builder
    .AddDefault("Example:Greeting", "Hello from TinyNet")
    .AddJsonConfig("config.json")
    .AddEnvironmentVariables("TINYNET_");

builder.RegisterMiddleware<RequestIdMiddleware>();

// EN: Singleton — one instance for the whole application, shared by parallel requests.
//     Scoped — one instance per HTTP request.
//     Handler classes and filters are not registered here: the framework registers them from the routes below.
// RU: Singleton — один экземпляр на всё приложение, общий для параллельных запросов.
//     Scoped — один экземпляр на HTTP-запрос.
//     Классы обработчиков и фильтры здесь не регистрируются: фреймворк сам регистрирует их из маршрутов ниже.
builder.Services.AddSingleton<VisitCounter>();
builder.Services.AddSingleton<NoteStore>();
builder.Services.AddScoped<RequestId>();

// EN: Routes are the only place where paths are declared. An endpoint is a method + path + handler.
//     "h => h.Index" picks a method of a handler class; the compiler checks that it matches
//     (HttpRequest, HttpContext) -> Task<HttpResponse>. The class is created from DI for every request.
// RU: Маршруты — единственное место, где объявляются пути. Эндпоинт — это метод + путь + обработчик.
//     "h => h.Index" выбирает метод класса-обработчика; компилятор проверяет, что он подходит под
//     (HttpRequest, HttpContext) -> Task<HttpResponse>. Класс создаётся из DI на каждый запрос.
builder.Routes.AddGet<HomeHandler>("/", h => h.Index);

// EN: A handler does not need a class: any delegate with the same signature works. Nothing is registered in DI.
// RU: Обработчику не обязателен класс: подходит любой делегат с той же сигнатурой. В DI ничего не регистрируется.
builder.Routes.AddGet("/health", (request, context) =>
    Task.FromResult(context.Response().Text("ok")));

// EN: A group adds a path prefix and filters. A group filter applies to every endpoint inside it,
//     including nested groups; filters run from the outer group to the inner one, then the endpoint's own.
//     One handler class may serve several paths: "/" and "/{id}" below both go to NotesHandler.
// RU: Группа добавляет префикс пути и фильтры. Фильтр группы действует на все эндпоинты внутри неё,
//     включая вложенные группы; фильтры идут от внешней группы к внутренней, затем фильтры эндпоинта.
//     Один класс-обработчик может обслуживать несколько путей: "/" и "/{id}" ниже ведут в NotesHandler.
builder.Routes
    .AddGroup("/api")
    .AddFilter<ApiKeyFilter>()
    .AddGroup("/notes")
    .AddGet<NotesHandler>("/", h => h.List)
    .AddPost<NotesHandler>("/", h => h.Create)
    .AddGet<NotesHandler>("/{id}", h => h.Get)
    .AddPut<NotesHandler>("/{id}", h => h.Replace)
    .AddPatch<NotesHandler>("/{id}", h => h.Update)
    // EN: The last argument configures one endpoint only: AdminFilter guards DELETE, not GET.
    //     A known path with an unregistered method answers 405 with an Allow header, an unknown path answers 404.
    // RU: Последний аргумент настраивает только один эндпоинт: AdminFilter защищает DELETE, но не GET.
    //     На известный путь с незарегистрированным методом ответ 405 с заголовком Allow, на неизвестный путь — 404.
    .AddDelete<NotesHandler>("/{id}", h => h.Delete, endpoint => endpoint.AddFilter<AdminFilter>());

// EN: Build checks everything at startup: duplicate routes, a filter applied twice,
//     a singleton that captures a scoped service. Mistakes fail here, not on the first request.
// RU: Build проверяет всё при старте: дубли маршрутов, фильтр, навешанный дважды,
//     синглтон, захвативший scoped-сервис. Ошибки падают здесь, а не на первом запросе.
var app = builder.Build();
await app.Run();