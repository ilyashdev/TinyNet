using System.Reflection;
using System.Text.Json;
using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.DI;
using TinyNet.Http;
using TinyNet.TaskResult;

namespace TinyNet.Controllers;

public class ControllerHandler
{
    private Dictionary<string, Type> _controllers = new();
    private DIContainer _container;
    private bool _initstate = false;
    
    
    
    public ControllerHandler(DIContainer container)
    {
        _container = container;
    }

    internal void InitControllers()
    {
        if (_initstate)
            throw new Exception("controllers cant be initialized 2nd time");
        _initstate = true;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var controllerTypes = assemblies
            .SelectMany(assembly => 
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(t => t.IsSubclassOf(typeof(Controller)) && !t.IsAbstract)
            .ToList();
        foreach (var controllerType in controllerTypes)
        {
            if(controllerType.CustomAttributes.Any(a => a.AttributeType == typeof(NotMappedAttribute)))
                continue;
            var route = controllerType.GetCustomAttribute<RouteAttribute>()?.Url ??
                        throw new InvalidOperationException(
                            $"Controller {controllerType.Name} is missing [Route] attribute");
            _container.AddTransient(controllerType);
            _controllers.Add(
                route, 
                controllerType
                );
        }
    }

    public HandleResult<Type> GetTypeHandler(string url)
    {
        if (url.Contains("."))
            return new HandleResult<Type>(typeof(MediaHandler));
        if (!_controllers.TryGetValue(url, out var type))
            return new HandleResult<Type>(HandleResultStatus.NotFound);
        return new HandleResult<Type>(type);
    }

     public Controller? GetController(string url, DIScope scope)                                                                                                                                                                                       
  {                                                                                                                                                                                                                                                 
      var type = GetTypeHandler(url);                                                                                                                                                                                                               
      if (type.Status != HandleResultStatus.Success)                                                                                                                                                                                                
          return null;                                                                                                                                                                                                                              
      return (Controller)_container.GetService(type.Result, scope);                                                                                                                                                                                 
  }  

    public async Task Handle(HttpContext httpContext, DIScope scope)
    {
        Controller controller = GetController(httpContext.Request.Url, scope);
        if (controller == null)
        {
            new BadRequest("Not Found endpoint").ExecuteResult(httpContext);
            return;
        };
        controller.SetContext(httpContext);
        var methods = controller.GetType()
            .GetMethods()
            .Where(m => 
            {
                var attributes = m.GetCustomAttributes<HttpMethodAttribute>();
                return attributes.Any(attr => 
                    attr.Method.Equals(httpContext.Request.Method, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
        if (methods.Count() > 1)
            throw new Exception("Multiple HTTP methods found");
        if (methods.Count() == 0)
        { 
            new BadRequest("No HTTP method handler").ExecuteResult(httpContext);
            return;
        }
        var method = methods.First();
        var parameters = method.GetParameters().ToList();
        var query = httpContext.Request.Query;
        var body = httpContext.Request.Body;
        var args = new object[parameters.Count];
        foreach (var param in parameters)
        {
            if (param.GetCustomAttribute<FromBodyAttribute>() != null)
            {
                var arg = JsonSerializer.Deserialize(body[param.Name], param.ParameterType);
                if (arg == null)
                {
                    new BadRequest($"No body argument found -- {param.Name}").ExecuteResult(httpContext);
                    return;
                };
                args[param.Position] = arg;
            }else if (param.GetCustomAttribute<FromQueryAttribute>() != null)
            {
                string? arg;
                query.TryGetValue(param.Name,out arg);
                if (arg == null)
                {
                    new BadRequest($"No query argument found -- {param.Name}").ExecuteResult(httpContext);
                    return;
                };
                args[param.Position] = arg;
            }
        }
        try
        {
            var result = method.Invoke(controller, args);
            if (result is Task taskResult)
            {
                await taskResult.ConfigureAwait(false);
                if (taskResult.GetType().IsGenericType)
                {
                    var resultValue = (IActionResult)taskResult.GetType()                                                                                                                                                                                         
                        .GetProperty("Result")!                                                                                                                                                                                                                   
                        .GetValue(taskResult)!;                                                                                                                                                                                                                    
                    resultValue.ExecuteResult(httpContext);                                                                                                                                                                                                   
                    return; 
                }
                new Ok("").ExecuteResult(httpContext);
                return;
            }
            ((IActionResult)result).ExecuteResult(httpContext);
            return;
        }
        catch (TargetInvocationException ex)
        {
            throw new Exception(ex.InnerException?.Message ?? "Unknown error");
        }
    }
}

