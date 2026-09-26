using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Configurations;
using TinyNet.Controllers;
using TinyNet.Example.Services;
using TinyNet.Http;

namespace TinyNet.Example.Controllers;

// EN: A controller is any class that implements handler interfaces: IGetHandler, IPostHandler and so on.
//     The framework registers it as transient and creates a new one for every request,
//     so the constructor may take services of any lifetime, including scoped ones.
// RU: Контроллер — любой класс, реализующий интерфейсы обработчиков: IGetHandler, IPostHandler и т.д.
//     Фреймворк регистрирует его как transient и создаёт заново на каждый запрос,
//     поэтому в конструктор можно брать сервисы любого времени жизни, включая scoped.
public class HomeController : IGetHandler
{
    private readonly VisitCounter _counter;
    private readonly RequestId _requestId;
    private readonly string _greeting;

    public HomeController(VisitCounter counter, RequestId requestId, IConfiguration configuration)
    {
        _counter = counter;
        _requestId = requestId;
        _greeting = configuration["Example:Greeting"] ?? "TinyNet";
    }

    public Task<IActionResult> Get(HttpContext context)
        => Task.FromResult<IActionResult>(new HtmlView(
            $"<h1>{_greeting}</h1><p>Visit #{_counter.Next()}, request {_requestId.Value}</p>"));
}