using TinyNet.Configurations;
using TinyNet.Controllers;
using TinyNet.Http;

namespace TinyNet.Tests;

public class StaticFileTests : IDisposable
{
    private readonly DirectoryInfo _temp;
    private readonly string _webRoot;
    private readonly string _secret;

    public StaticFileTests()
    {
        _temp = Directory.CreateTempSubdirectory("tinynet-webroot-");
        _webRoot = Path.Combine(_temp.FullName, "wwwroot");
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<h1>ok</h1>");

        _secret = Path.Combine(_temp.FullName, "secret.txt");
        File.WriteAllText(_secret, "TOP SECRET");
    }

    [Fact]
    public async Task ExistingFileInsideWebRoot_IsServed()
    {
        var response = await RequestAsync("/index.html");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("text/html", response.Headers["Content-Type"]);
        Assert.Equal("<h1>ok</h1>", System.Text.Encoding.UTF8.GetString(response.BinaryBody!));
    }

    [Theory]
    [InlineData("/../secret.txt")]
    [InlineData("/..%2fsecret.txt")]
    [InlineData("/%2e%2e/secret.txt")]
    [InlineData("/..\\secret.txt")]
    [InlineData("/wwwroot/../../secret.txt")]
    public async Task PathTraversal_IsRejected(string url)
    {
        var response = await RequestAsync(url);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task AbsolutePath_IsRejected()
    {
        var response = await RequestAsync("/" + _secret.Replace('\\', '/'));

        Assert.Equal(404, response.StatusCode);
    }

    private async Task<HttpResponse> RequestAsync(string url)
    {
        var configuration = new ConfigurationBuilder()
            .AddDefault("WebRoot:Path", _webRoot)
            .Build();

        var handler = new MediaHandler(configuration);
        var context = new HttpContext(new HttpRequest("GET", url, new(), new(), null));
        handler.SetContext(context);

        var result = await handler.Get();
        result.ExecuteResult(context);

        return context.Response!;
    }

    public void Dispose() => _temp.Delete(recursive: true);
}