using System.Diagnostics.CodeAnalysis;
using System.Text;
using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Configurations;

namespace TinyNet.Controllers;
[NotMapped]
public class MediaHandler : Controller
{
    internal static Dictionary<string,string> StaticContent = new()
    {
        {"html","text/html"},
        {"css","text/css"},
        {"jpeg","image/jpeg"},
        {"jpg","image/jpeg"},
        {"png","image/png"},
        {"bmp","image/bmp"},
        {"gif","image/gif"},
        {"tiff","image/tiff"},
        {"tif","image/tiff"},
        {"webp","image/webp"},
        {"json","application/json"},
        {"xml","application/xml"},
        {"zip","application/zip"},
        {"rar","application/rar"},
        {"js", "application/javascript"},
    };
    
    private readonly string _webRoot;

    public MediaHandler(IConfiguration config)
    {
        _webRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            (config["WebRoot:Path"] ?? "/WebRoot").TrimStart('/', '\\')));
    }

    [HttpMethod("GET")]
    public async Task<IActionResult> Get()
    {
        if (!TryResolvePath(_context.Request.Url, out var path) || !File.Exists(path))
            return new NotFound();
        string extension = Path.GetExtension(path).TrimStart('.').ToLower();
        if (!StaticContent.TryGetValue(extension, out string contentType))
            contentType = "application/octet-stream";
        byte[] fileData = await File.ReadAllBytesAsync(path);
        return new Media(fileData, contentType);
    }

    private bool TryResolvePath(string url, [NotNullWhen(true)] out string? fullPath)
    {
        fullPath = null;

        var relative = Uri.UnescapeDataString(url).TrimStart('/', '\\');
        if (relative.Length == 0)
            return false;
        if (relative.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return false;
        if (Path.IsPathRooted(relative))
            return false; 

        var candidate = Path.GetFullPath(Path.Combine(_webRoot, relative));
        
        var rootWithSeparator = _webRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _webRoot
            : _webRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            return false;

        fullPath = candidate;
        return true;
    }
}