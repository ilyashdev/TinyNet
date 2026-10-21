using System.Text;
using TinyNet.Configurations;
using TinyNet.Example.Services;
using TinyNet.Http;

namespace TinyNet.Example.Pages;

// EN: A handler class is created from DI for every request (transient), so its constructor
//     may take services of any lifetime, including scoped ones like RequestId.
// RU: Класс-обработчик создаётся из DI на каждый запрос (transient), поэтому в конструктор
//     можно брать сервисы любого времени жизни, включая scoped, как RequestId.
public class NotesPage
{
    private readonly NoteStore _store;
    private readonly RequestId _requestId;
    private readonly string _title;

    public NotesPage(NoteStore store, RequestId requestId, IConfiguration configuration)
    {
        _store = store;
        _requestId = requestId;
        _title = configuration["Example:Title"] ?? "Notes";
    }

    // EN: A handler that does not read the body may return HttpResponse directly, without a Task.
    // RU: Обработчик, который не читает тело, может вернуть HttpResponse сразу, без Task.
    public HttpResponse Index(HttpRequest request, HttpContext context)
    {
        var error = request.GetFromQuery("error") == "title"
            ? """<p class="error">A note needs a title.</p>"""
            : "";
        var notes = string.Concat(_store.All().Select(note => $"""
            <div class="note">
              <div><strong>{Layout.Encode(note.Title)}</strong><p>{Layout.Encode(note.Text)}</p></div>
              <form method="post" action="/notes/{note.Id}/delete"><button>Delete</button></form>
            </div>
            """));

        return Html(context, Layout.Page(_title, _requestId.Value, $"""
            {error}
            <form class="add" method="post" action="/notes">
              <input name="title" placeholder="Title">
              <textarea name="text" rows="3" placeholder="Text"></textarea>
              <button>Add note</button>
            </form>
            {(notes.Length == 0 ? "<p>No notes yet.</p>" : notes)}
            """));
    }

    // EN: The framework has no form binding, and the body can be read only asynchronously:
    //     the handler awaits the body, parses the form itself and returns Task<HttpResponse>.
    // RU: Привязки форм во фреймворке нет, а тело читается только асинхронно:
    //     обработчик ждёт тело, сам разбирает форму и возвращает Task<HttpResponse>.
    public async Task<HttpResponse> Create(HttpRequest request, HttpContext context)
    {
        var form = Form.Parse(await request.BufferBodyAsync());
        var title = form.GetValueOrDefault("title")?.Trim();
        if (string.IsNullOrEmpty(title))
            return Redirect(context, "/?error=title");

        _store.Add(title, form.GetValueOrDefault("text") ?? "");
        return Redirect(context, "/");
    }

    // EN: {id} comes from the route "/notes/{id}/delete". When it is not a valid int,
    //     the framework answers 400 and the handler does not continue.
    // RU: {id} берётся из маршрута "/notes/{id}/delete". Если он не разбирается в int,
    //     фреймворк отвечает 400 и обработчик дальше не выполняется.
    public HttpResponse Delete(HttpRequest request, HttpContext context)
    {
        _store.Remove(request.GetFromRoute<int>("id")!.Value);
        return Redirect(context, "/");
    }

    private static HttpResponse Html(HttpContext context, string html)
        => context.Response().Bytes(Encoding.UTF8.GetBytes(html), $"{MediaTypes.TextHtml}; charset=utf-8");

    private static HttpResponse Redirect(HttpContext context, string location)
        => context.Response().Status(StatusCodes.SeeOther).AddHeader(HeaderNames.Location, location).Empty();
}