using TinyNet.ActionResult;
using TinyNet.Controllers;
using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Routing;

public sealed class GroupRoute
{
    private readonly List<Type> _filters = new();
    private readonly List<EndpointRoute> _endpoints = new();
    private readonly List<GroupRoute> _groups = new();
    private GroupRoute? _parent;
    private bool _frozen;

    public GroupRoute(string basePath)
    {
        BasePath = basePath;
    }

    public string BasePath { get; }
    internal IReadOnlyList<Type> Filters => _filters;
    internal IReadOnlyList<EndpointRoute> Endpoints => _endpoints;
    internal IReadOnlyList<GroupRoute> Groups => _groups;

    internal void Freeze() => _frozen = true;

    public GroupRoute AddFilter<T>() where T : IMiddleware
    {
        EnsureNotFrozen();
        _filters.Add(typeof(T));
        return this;
    }

    public GroupRoute AddGroup(string basePath)
        => AddGroup(new GroupRoute(basePath));

    public GroupRoute AddGroup(GroupRoute group)
    {
        EnsureNotFrozen();
        if (group._parent is not null)
            throw new InvalidOperationException($"Group {group.BasePath} already belongs to group {group._parent.BasePath}");
        for (var ancestor = this; ancestor is not null; ancestor = ancestor._parent)
            if (ancestor == group)
                throw new InvalidOperationException($"Group {group.BasePath} cannot contain itself");
        group._parent = this;
        _groups.Add(group);
        return group;
    }

    public GroupRoute AddGetHandler<T>(Action<EndpointRoute>? setup = null) where T : class, IGetHandler
        => AddEndpoint<T>("GET", (controller, context) => controller.Get(context), setup);

    public GroupRoute AddPostHandler<T>(Action<EndpointRoute>? setup = null) where T : class, IPostHandler
        => AddEndpoint<T>("POST", (controller, context) => controller.Post(context), setup);

    public GroupRoute AddPutHandler<T>(Action<EndpointRoute>? setup = null) where T : class, IPutHandler
        => AddEndpoint<T>("PUT", (controller, context) => controller.Put(context), setup);

    public GroupRoute AddPatchHandler<T>(Action<EndpointRoute>? setup = null) where T : class, IPatchHandler
        => AddEndpoint<T>("PATCH", (controller, context) => controller.Patch(context), setup);

    public GroupRoute AddDeleteHandler<T>(Action<EndpointRoute>? setup = null) where T : class, IDeleteHandler
        => AddEndpoint<T>("DELETE", (controller, context) => controller.Delete(context), setup);

    private GroupRoute AddEndpoint<T>(
        string method, Func<T, HttpContext, Task<IActionResult>> action, Action<EndpointRoute>? setup) where T : class
    {
        EnsureNotFrozen();
        if (_endpoints.Any(e => e.Method == method))
            throw new InvalidOperationException($"Group {BasePath} already has a {method} handler");
        var endpoint = new EndpointRoute(method, typeof(T), async context =>
        {
            var controller = context.GetService<T>();
            var result = await action(controller, context);
            result.ExecuteResult(context);
        });
        setup?.Invoke(endpoint);
        _endpoints.Add(endpoint);
        return this;
    }

    private void EnsureNotFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException($"Group {BasePath} cannot be changed after Build()");
    }
}