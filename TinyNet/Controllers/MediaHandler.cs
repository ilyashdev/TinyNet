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
    
    private IConfiguration _config;

    public MediaHandler(IConfiguration config)
    {
        _config = config;
    }

    [HttpMethod("GET")]
    public async Task<IActionResult> Get()
    {
        var path = Path.GetFullPath(Directory.GetCurrentDirectory() + _config["WebRoot:Path"] + _context.Request.Url);
        if (!File.Exists(path))
            return new NotFound();
        string extension = Path.GetExtension(path).TrimStart('.').ToLower();
        if (!StaticContent.TryGetValue(extension, out string contentType))
            contentType = "application/octet-stream";
        byte[] fileData = await File.ReadAllBytesAsync(path);
        if (contentType.StartsWith("text/") ||
            contentType == "application/xml" ||
            contentType == "application/json" ||
            contentType == "application/javascript")
            return new Media(Encoding.UTF8.GetString(fileData), contentType);
        else
        {
            return new Media(Encoding.ASCII.GetString(fileData), contentType);
        }
        
    }
}