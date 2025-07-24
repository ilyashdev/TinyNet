using TinyNet.Http;

namespace TinyNet.Controllers;

public abstract class Controller
{
    protected HttpContext _context;
    
    internal void SetContext(HttpContext context)
    => _context = context;
}