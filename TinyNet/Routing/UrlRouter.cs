namespace TinyNet.Routing;

public enum RouteStatus
{
    Found,
    MethodNotAllowed,
    NotFound
}

public readonly record struct RouteMatch(RouteStatus Status, Endpoint? Endpoint, Dictionary<string, string> Values);

public class UrlRouter
{
    private const int MaxSegments = 64;

    private readonly Node rootNode = new(string.Empty);
    private class Node
    {
        public Node(string Name)
        {
            this.Name = Name;
        }
        public readonly string Name;
        public readonly Dictionary<string, Endpoint> Endpoints = new(StringComparer.Ordinal);
        public Node? ParamSubNode = null;
        public Dictionary<string, Node> Static { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
    public UrlRouter(IEnumerable<Endpoint> endpoints)
    {
        foreach (var endpoint in endpoints)
            Add(endpoint);
    }

    public RouteMatch Match(string method, string url)
    {
        var segments = SplitUrl(url);
        if (segments is null)
            return new RouteMatch(RouteStatus.NotFound, null, new());
        var values = new List<KeyValuePair<string, string>>();
        var pathFound = false;
        var node = Match(rootNode, segments, 0, method, values, ref pathFound);
        if (node is null)
            return new RouteMatch(pathFound ? RouteStatus.MethodNotAllowed : RouteStatus.NotFound, null, new());
        return new RouteMatch(RouteStatus.Found, node.Endpoints[method], values.ToDictionary());
    }

    private static Node? Match(
        Node node, string[] segments, int index, string method, List<KeyValuePair<string, string>> values, ref bool pathFound)
    {
        if (index == segments.Length)
        {
            if (node.Endpoints.Count == 0)
                return null;
            pathFound = true;
            return node.Endpoints.ContainsKey(method) ? node : null;
        }
        if (node.Static.TryGetValue(segments[index], out var staticNode))
        {
            var staticMatch = Match(staticNode, segments, index + 1, method, values, ref pathFound);
            if (staticMatch is not null)
                return staticMatch;
        }
        if (node.ParamSubNode is null)
            return null;
        values.Add(new KeyValuePair<string, string>(node.ParamSubNode.Name, segments[index]));
        var paramMatch = Match(node.ParamSubNode, segments, index + 1, method, values, ref pathFound);
        if (paramMatch is not null)
            return paramMatch;
        values.RemoveAt(values.Count - 1);
        return null;
    }

    private static string[]? SplitUrl(string url)
    {
        var segments = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > MaxSegments)
            return null;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = Uri.UnescapeDataString(segments[i]);
            if (segment is "." or ".." || segment.Contains('/'))
                return null;
            segments[i] = segment;
        }
        return segments;
    }

    private void Add(Endpoint endpoint)
    {
        var dirs = endpoint.Template.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var names = new HashSet<string>();
        var node = rootNode;
        foreach (var dirName in dirs)
        {
            if (dirName.StartsWith("{"))
            {
                if (!dirName.EndsWith("}"))
                    throw new InvalidOperationException($"{endpoint}: invalid segment {dirName}");
                var paramName = dirName.Substring(1, dirName.Length - 2);
                if (paramName.Length == 0 || paramName.Contains('{') || paramName.Contains('}'))
                    throw new InvalidOperationException($"{endpoint}: invalid route parameter name {dirName}");
                if (!names.Add(paramName))
                    throw new InvalidOperationException($"{endpoint}: route parameter {paramName} is repeated");
                if (node.ParamSubNode is null)
                    node.ParamSubNode = new Node(paramName);
                else if (node.ParamSubNode.Name != paramName)
                    throw new InvalidOperationException(
                        $"{endpoint}: conflicting route parameter names {node.ParamSubNode.Name} -- {paramName}");
                node = node.ParamSubNode;
            }
            else if (dirName.Contains('{') || dirName.Contains('}'))
            {
                throw new InvalidOperationException($"{endpoint}: invalid segment {dirName}");
            }
            else if (node.Static.TryGetValue(dirName, out var staticNode))
            {
                node = staticNode;
            }
            else
            {
                var newNode = new Node(dirName);
                node.Static.Add(dirName, newNode);
                node = newNode;
            }
        }
        if (!node.Endpoints.TryAdd(endpoint.Method, endpoint))
            throw new InvalidOperationException($"Duplicate route: {endpoint}");
    }
}