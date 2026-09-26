using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;
using TinyNet.Example.Models;
using TinyNet.Example.Services;
using TinyNet.Http;

namespace TinyNet.Example.Controllers;

public class NoteController : IGetHandler, IPutHandler, IPatchHandler, IDeleteHandler
{
    private readonly NoteStore _store;

    public NoteController(NoteStore store)
    {
        _store = store;
    }

    public Task<IActionResult> Get(HttpContext context)
    {
        // EN: GetFromRoute reads a {placeholder} from the group path "/api/notes/{id}".
        //     It returns null when the value is not a valid int; the controller decides how to answer.
        // RU: GetFromRoute читает {плейсхолдер} из пути группы "/api/notes/{id}".
        //     Он возвращает null, если значение не разбирается в int; как ответить, решает контроллер.
        var id = context.GetFromRoute<int>("id");
        if (id is null)
            return Task.FromResult(InvalidId());
        return Task.FromResult(Found(_store.Find(id.Value)));
    }

    public async Task<IActionResult> Put(HttpContext context)
    {
        var id = context.GetFromRoute<int>("id");
        if (id is null)
            return InvalidId();
        var input = await context.ReadFromBodyAsync<NoteInput>();
        if (input is null || string.IsNullOrWhiteSpace(input.Title))
            return new BadRequest("Body must be a note with a Title");
        return Found(_store.Update(id.Value, note => note with { Title = input.Title, Text = input.Text }));
    }

    public async Task<IActionResult> Patch(HttpContext context)
    {
        var id = context.GetFromRoute<int>("id");
        if (id is null)
            return InvalidId();
        var patch = await context.ReadFromBodyAsync<NotePatch>();
        if (patch is null)
            return new BadRequest("Body must be a JSON object");
        return Found(_store.Update(id.Value, note => note with
        {
            Title = patch.Title ?? note.Title,
            Text = patch.Text ?? note.Text
        }));
    }

    public Task<IActionResult> Delete(HttpContext context)
    {
        var id = context.GetFromRoute<int>("id");
        if (id is null)
            return Task.FromResult(InvalidId());
        return Task.FromResult<IActionResult>(_store.Remove(id.Value) ? new BaseResult(204) : new NotFound());
    }

    private static IActionResult InvalidId()
        => new BadRequest("id must be a number");

    private static IActionResult Found(Note? note)
        => note is null ? new NotFound() : new Ok(note);
}