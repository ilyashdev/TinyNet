using TinyNet.Example.Models;
using TinyNet.Example.Services;
using TinyNet.Http;

namespace TinyNet.Example.Handlers;

public class NotesHandler
{
    private readonly NoteStore _store;

    public NotesHandler(NoteStore store)
    {
        _store = store;
    }

    public Task<HttpResponse> List(HttpRequest request, HttpContext context)
    {
        // EN: GetFromQuery<int> returns null when the key is missing; ?? picks the default.
        //     A value that is not a number (?take=many) is answered 400 by the framework.
        // RU: GetFromQuery<int> возвращает null, если ключа нет; ?? подставляет значение по умолчанию.
        //     На значение, которое не число (?take=many), фреймворк сам отвечает 400.
        var take = request.GetFromQuery<int>("take") ?? 10;
        return Task.FromResult(context.Response().Json(_store.List(take)));
    }

    public async Task<HttpResponse> Create(HttpRequest request, HttpContext context)
    {
        // EN: ReadJsonAsync ignores property name case. A body that is not valid JSON is answered 400
        //     by the framework; the exception still passes through the middleware on its way out.
        // RU: ReadJsonAsync не различает регистр имён свойств. На тело, которое не является JSON,
        //     фреймворк сам отвечает 400; исключение при этом проходит обратно через middleware.
        var input = await request.ReadJsonAsync<NoteInput>();
        if (input is null || string.IsNullOrWhiteSpace(input.Title))
            return TitleRequired(context);
        return context.Response().Status(StatusCodes.Created).Json(_store.Add(input));
    }

    public Task<HttpResponse> Get(HttpRequest request, HttpContext context)
        => Task.FromResult(Found(context, _store.Find(Id(request))));

    public async Task<HttpResponse> Replace(HttpRequest request, HttpContext context)
    {
        var id = Id(request);
        var input = await request.ReadJsonAsync<NoteInput>();
        if (input is null || string.IsNullOrWhiteSpace(input.Title))
            return TitleRequired(context);
        return Found(context, _store.Update(id, note => note with { Title = input.Title, Text = input.Text }));
    }

    public async Task<HttpResponse> Update(HttpRequest request, HttpContext context)
    {
        var id = Id(request);
        var patch = await request.ReadJsonAsync<NotePatch>();
        if (patch is null)
            return context.Response().Status(StatusCodes.BadRequest).Text("Body must be a JSON object");
        return Found(context, _store.Update(id, note => note with
        {
            Title = patch.Title ?? note.Title,
            Text = patch.Text ?? note.Text
        }));
    }

    public Task<HttpResponse> Delete(HttpRequest request, HttpContext context)
        => Task.FromResult(_store.Remove(Id(request))
            ? context.Response().Status(StatusCodes.NoContent).Empty()
            : context.Response().Status(StatusCodes.NotFound).Empty());

    // EN: GetFromRoute reads a {placeholder} from the route "/api/notes/{id}", so the value is always present.
    //     When it is not a valid int, the framework answers 400 and the handler does not continue.
    // RU: GetFromRoute читает {плейсхолдер} из маршрута "/api/notes/{id}", поэтому значение есть всегда.
    //     Если оно не разбирается в int, фреймворк отвечает 400 и обработчик дальше не выполняется.
    private static int Id(HttpRequest request)
        => request.GetFromRoute<int>("id")!.Value;

    private static HttpResponse TitleRequired(HttpContext context)
        => context.Response().Status(StatusCodes.BadRequest).Text("Body must be a note with a Title");

    private static HttpResponse Found(HttpContext context, Note? note)
        => note is null
            ? context.Response().Status(StatusCodes.NotFound).Empty()
            : context.Response().Json(note);
}
