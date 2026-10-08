using TinyNet.DI;

namespace TinyNet.Routing;

internal static class RouteCompiler
{
    public static List<Endpoint> Compile(GroupRoute root, DIContainer container)
    {
        var endpoints = new List<Endpoint>();
        Walk(root, string.Empty, new List<Type>(), endpoints);
        foreach (var controller in Controllers(root).Distinct())
            container.AddTransient(controller);
        foreach (var filter in endpoints.SelectMany(e => e.Filters).Distinct())
            if (!container.IsService(filter))
                container.AddSingleton(filter);
        return endpoints;
    }

    private static void Walk(GroupRoute group, string prefix, List<Type> inherited, List<Endpoint> endpoints)
    {
        group.Freeze();
        var path = Join(prefix, group.BasePath);
        var filters = inherited.Concat(group.Filters).ToList();
        foreach (var route in group.Endpoints)
        {
            route.Freeze();
            endpoints.Add(new Endpoint(route, Join(path, route.Path), filters.Concat(route.Filters).ToList()));
        }

        foreach (var child in group.Groups)
            Walk(child, path, filters, endpoints);
    }

    private static IEnumerable<Type> Controllers(GroupRoute group)
        => group.Endpoints.Select(e => e.Controller).OfType<Type>().Concat(group.Groups.SelectMany(Controllers));

    private static string Join(string prefix, string path)
        => "/" + string.Join('/', $"{prefix}/{path}".Split('/', StringSplitOptions.RemoveEmptyEntries));
}