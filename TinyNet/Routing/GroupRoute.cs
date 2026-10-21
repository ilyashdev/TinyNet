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
            throw new InvalidOperationException(
                $"Group {group.BasePath} already belongs to group {group._parent.BasePath}");
        for (var ancestor = this; ancestor is not null; ancestor = ancestor._parent)
            if (ancestor == group)
                throw new InvalidOperationException($"Group {group.BasePath} cannot contain itself");
        group._parent = this;
        _groups.Add(group);
        return group;
    }

    public GroupRoute AddGet(string path, RequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddEndpoint(nameof(HttpMethods.GET), path, null, handler, setup);
    public GroupRoute AddGet<T>(string path, Func<T, RequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddEndpoint(nameof(HttpMethods.GET), path, handler, setup);

    public GroupRoute AddPost(string path, RequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddEndpoint(nameof(HttpMethods.POST), path, null, handler, setup);

    public GroupRoute AddPost<T>(string path, Func<T, RequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddEndpoint(nameof(HttpMethods.POST), path, handler, setup);

    public GroupRoute AddPut(string path, RequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddEndpoint(nameof(HttpMethods.PUT), path, null, handler, setup);

    public GroupRoute AddPut<T>(string path, Func<T, RequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddEndpoint(nameof(HttpMethods.PUT), path, handler, setup);

    public GroupRoute AddPatch(string path, RequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddEndpoint(nameof(HttpMethods.PATCH), path, null, handler, setup);

    public GroupRoute AddPatch<T>(string path, Func<T, RequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddEndpoint(nameof(HttpMethods.PATCH), path, handler, setup);

    public GroupRoute AddDelete(string path, RequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddEndpoint(nameof(HttpMethods.DELETE), path, null, handler, setup);

    public GroupRoute AddDelete<T>(string path, Func<T, RequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddEndpoint(nameof(HttpMethods.DELETE), path, handler, setup);

    public GroupRoute AddGet(string path, SyncRequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddGet(path, ToAsync(handler), setup);

    public GroupRoute AddGet<T>(string path, Func<T, SyncRequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddGet<T>(path, controller => ToAsync(handler(controller)), setup);

    public GroupRoute AddPost(string path, SyncRequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddPost(path, ToAsync(handler), setup);

    public GroupRoute AddPost<T>(string path, Func<T, SyncRequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddPost<T>(path, controller => ToAsync(handler(controller)), setup);

    public GroupRoute AddPut(string path, SyncRequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddPut(path, ToAsync(handler), setup);

    public GroupRoute AddPut<T>(string path, Func<T, SyncRequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddPut<T>(path, controller => ToAsync(handler(controller)), setup);

    public GroupRoute AddPatch(string path, SyncRequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddPatch(path, ToAsync(handler), setup);

    public GroupRoute AddPatch<T>(string path, Func<T, SyncRequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddPatch<T>(path, controller => ToAsync(handler(controller)), setup);

    public GroupRoute AddDelete(string path, SyncRequestDelegate handler, Action<EndpointRoute>? setup = null)
        => AddDelete(path, ToAsync(handler), setup);

    public GroupRoute AddDelete<T>(string path, Func<T, SyncRequestDelegate> handler, Action<EndpointRoute>? setup = null)
        where T : class
        => AddDelete<T>(path, controller => ToAsync(handler(controller)), setup);

    private static RequestDelegate ToAsync(SyncRequestDelegate handler)
        => (request, context) => Task.FromResult(handler(request, context));

    private GroupRoute AddEndpoint<T>(
        string method, string path, Func<T, RequestDelegate> handler, Action<EndpointRoute>? setup) where T : class
        => AddEndpoint(method, path, typeof(T),
            (request, context) => handler(context.GetService<T>())(request, context), setup);

    private GroupRoute AddEndpoint(
        string method, string path, Type? controller, RequestDelegate handler, Action<EndpointRoute>? setup)
    {
        EnsureNotFrozen();
        var endpoint = new EndpointRoute(method, path, controller, handler);
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