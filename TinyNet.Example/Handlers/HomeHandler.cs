using System.Text;
using TinyNet.Configurations;
using TinyNet.Example.Services;
using TinyNet.Http;

namespace TinyNet.Example.Handlers;

// EN: A handler class is created from DI for every request (transient), so its constructor
//     may take services of any lifetime, including scoped ones like RequestId.
// RU: Класс-обработчик создаётся из DI на каждый запрос (transient), поэтому в конструктор
//     можно брать сервисы любого времени жизни, включая scoped, как RequestId.
public class HomeHandler
{
    private readonly VisitCounter _counter;
    private readonly RequestId _requestId;
    private readonly string _greeting;

    public HomeHandler(VisitCounter counter, RequestId requestId, IConfiguration configuration)
    {
        _counter = counter;
        _requestId = requestId;
        _greeting = configuration["Example:Greeting"] ?? "TinyNet";
    }

    public Task<HttpResponse> Index(HttpRequest request, HttpContext context)
    {
        var html = $"<h1>{_greeting}</h1><p>Visit #{_counter.Next()}, request {_requestId.Value}</p>";

        // EN: context.Response() starts a response; the final call (Bytes, Json, Text, Empty) produces it.
        // RU: context.Response() начинает ответ; завершающий вызов (Bytes, Json, Text, Empty) его создаёт.
        return Task.FromResult(context.Response()
            .Bytes(Encoding.UTF8.GetBytes(html), $"{MediaTypes.TextHtml}; charset=utf-8"));
    }
}
