namespace TinyNet.Controllers;

public class UrlRouter
{
    private const int MaxSegments = 64;

    private Node rootNode;
    private class Node
    {
        public Node(string Name)
        {
            this.Name = Name;
        }
        public readonly string Name;
        public Type? Controller;
        public Node? ParamSubNode = null;
        public Dictionary<string, Node> Static { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
    public UrlRouter(List<(string, Type)> controllers)
    {
        rootNode = new Node(string.Empty);
        ParseControllers(controllers);
    }

    public Type? GetControllerType(string url)
    {
        return TryMatch(url, out var controller, out _) ? controller : null;
    }

    public bool TryMatch(string url, out Type? controller, out Dictionary<string, string> routeValues)
    {
        controller = null;
        routeValues = new Dictionary<string, string>();
        var segments = SplitUrl(url);
        if (segments is null)
            return false;
        var values = new List<KeyValuePair<string, string>>();
        var node = Match(rootNode, segments, 0, values);
        if (node is null)
            return false;
        controller = node.Controller;
        foreach (var value in values)
            routeValues[value.Key] = value.Value;
        return true;
    }

    private static Node? Match(Node node, string[] segments, int index, List<KeyValuePair<string, string>> values)
    {
        if (index == segments.Length)
            return node.Controller is null ? null : node;
        if (node.Static.TryGetValue(segments[index], out var staticNode))
        {
            var staticMatch = Match(staticNode, segments, index + 1, values);
            if (staticMatch is not null)
                return staticMatch;
        }
        if (node.ParamSubNode is null)
            return null;
        values.Add(new KeyValuePair<string, string>(node.ParamSubNode.Name, segments[index]));
        var paramMatch = Match(node.ParamSubNode, segments, index + 1, values);
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

    private void ParseControllers(List<(string, Type)> controllers)
    {
        foreach (var controller in controllers)
        {
            var dirs = controller.Item1.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var node = rootNode;
            foreach (var dirName in dirs)
            {
                if (dirName.StartsWith("{"))
                {
                    if (!dirName.EndsWith("}"))
                        throw new Exception($"Invalid direct controller name: {dirName}");
                    var paramName = dirName.Substring(1, dirName.Length - 2);
                    if (paramName.Length == 0 || paramName.Contains('{') || paramName.Contains('}'))
                        throw new Exception($"Invalid route parameter name: {dirName}");
                    if (node.ParamSubNode is null)
                        node.ParamSubNode = new Node(paramName);
                    else if (node.ParamSubNode.Name != paramName)
                        throw new Exception($"Conflicting route parameter names: {node.ParamSubNode.Name} -- {paramName}");
                    node = node.ParamSubNode;
                }
                else if (dirName.Contains('{') || dirName.Contains('}'))
                {
                    throw new Exception($"Invalid direct controller name: {dirName}");
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
            if (node.Controller is not null)
                throw new Exception($"Multi Path controller: {controller.Item1} -- {controller.Item2}");
            node.Controller = controller.Item2;
        }
    }
}
