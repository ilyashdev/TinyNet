using HttpMethod = TinyNet.Http.HttpMethod;

namespace TinyNet.Controllers;

[AttributeUsage(AttributeTargets.Method)]
public class HttpMethodAttribute : Attribute
{
    
    public string Method { get; }
    
    public HttpMethodAttribute(string method)
    {
        if (!HttpMethod.IsAllowed(method))
            throw new ArgumentException("not found http method");
        this.Method = method;
    }
}

