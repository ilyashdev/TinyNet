using TinyNet.Application;
using TinyNet.Example.Controllers;
using TinyNet.Example.Filters;
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
//     Controllers and filters are not registered here: the framework registers them from the routes below.
// RU: Singleton — один экземпляр на всё приложение, общий для параллельных запросов.
//     Scoped — один экземпляр на HTTP-запрос.
//     Контроллеры и фильтры здесь не регистрируются: фреймворк сам регистрирует их из маршрутов ниже.
builder.Services.AddSingleton<VisitCounter>();
builder.Services.AddSingleton<NoteStore>();
builder.Services.AddScoped<RequestId>();

// EN: Routes are the only place where paths are declared. Paths are built from groups only;
//     a handler is attached to the end of its group and returns the same group, so calls chain.
// RU: Маршруты — единственное место, где объявляются пути. Путь строится только из групп;
//     обработчик вешается на конец своей группы и возвращает ту же группу, поэтому вызовы идут цепочкой.
builder.Routes
    .AddGetHandler<HomeController>();

// EN: A group filter applies to every endpoint inside the group, including nested groups.
//     Filters run from the outer group to the inner one, then the endpoint's own filters.
// RU: Фильтр группы действует на все эндпоинты внутри неё, включая вложенные группы.
//     Фильтры выполняются от внешней группы к внутренней, затем идут фильтры самого эндпоинта.
var notes = builder.Routes
    .AddGroup("/api")
    .AddFilter<ApiKeyFilter>()
    .AddGroup("/notes")
    .AddGetHandler<NotesController>()
    .AddPostHandler<NotesController>();

// EN: "{id}" becomes a route value read with context.GetFromRoute<int>("id").
//     The setup delegate attaches a filter to one method only: AdminFilter guards DELETE, not GET.
//     A known path with an unregistered method answers 405, an unknown path answers 404.
// RU: "{id}" становится значением маршрута, которое читается через context.GetFromRoute<int>("id").
//     Делегат настройки навешивает фильтр только на один метод: AdminFilter защищает DELETE, но не GET.
//     На известный путь с незарегистрированным методом ответ 405, на неизвестный путь — 404.
notes.AddGroup("{id}")
    .AddGetHandler<NoteController>()
    .AddPutHandler<NoteController>()
    .AddPatchHandler<NoteController>()
    .AddDeleteHandler<NoteController>(endpoint => endpoint.AddFilter<AdminFilter>());

// EN: Build checks everything at startup: duplicate routes, a filter applied twice,
//     a singleton that captures a scoped service. Mistakes fail here, not on the first request.
// RU: Build проверяет всё при старте: дубли маршрутов, фильтр, навешанный дважды,
//     синглтон, захвативший scoped-сервис. Ошибки падают здесь, а не на первом запросе.
var app = builder.Build();
await app.Run();
