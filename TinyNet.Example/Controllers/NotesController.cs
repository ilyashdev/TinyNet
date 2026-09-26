using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;
using TinyNet.Example.Models;
using TinyNet.Example.Services;
using TinyNet.Http;

namespace TinyNet.Example.Controllers;

public class NotesController : IGetHandler, IPostHandler
{
    private readonly NoteStore _store;

    public NotesController(NoteStore store)
    {
        _store = store;
    }

    public Task<IActionResult> Get(HttpContext context)
    {
        // EN: GetFromQuery<int> returns null when the key is missing or the value is not a number; ?? picks the default.
        // RU: GetFromQuery<int> возвращает null, если ключа нет или значение не число; ?? подставляет значение по умолчанию.
        var take = context.GetFromQuery<int>("take") ?? 10;
        return Task.FromResult<IActionResult>(new Ok(_store.List(take)));
    }

    public async Task<IActionResult> Post(HttpContext context)
    {
        // EN: A malformed JSON body never reaches the controller: the framework answers 400 while reading the request.
        //     ReadFromBodyAsync returns null when there is no JSON body or it does not fit the type. Property names are case-insensitive.
        // RU: Битый JSON до контроллера не доходит: фреймворк отвечает 400 ещё при чтении запроса.
        //     ReadFromBodyAsync возвращает null, если JSON-тела нет или оно не подходит к типу. Имена свойств нечувствительны к регистру.
        var input = await context.ReadFromBodyAsync<NoteInput>();
        if (input is null || string.IsNullOrWhiteSpace(input.Title))
            return new BadRequest("Body must be a note with a Title");
        return new BaseResult(201, _store.Add(input));
    }
}