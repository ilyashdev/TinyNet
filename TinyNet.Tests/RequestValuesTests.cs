using System.Text;
using System.Text.Json;
using TinyNet.Exceptions;
using TinyNet.Http;

namespace TinyNet.Tests;

public class RequestValuesTests
{
    public record Person(string Name);

    private static HttpRequest Request(string body = "")
    {
        var settings = new HttpSettings(new JsonSerializerOptions(JsonSerializerDefaults.Web), 1024);
        var query = new Dictionary<string, string> { ["count"] = "5", ["broken"] = "five" };
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return new HttpRequest("POST", "/", "/", "HTTP/1.1", settings, new HttpHeaders(), query, stream,
            body.Length > 0, CancellationToken.None);
    }

    [Fact]
    public void GetFromQuery_MissingValue_ReturnsNull()
    {
        Assert.Null(Request().GetFromQuery<int>("missing"));
    }

    [Fact]
    public void GetFromQuery_UnparsableValue_Throws400()
    {
        var error = Assert.Throws<RequestValueException>(() => Request().GetFromQuery<int>("broken"));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal(5, Request().GetFromQuery<int>("count"));
    }

    [Fact]
    public async Task ReadJsonAsync_IgnoresPropertyNameCase()
    {
        var person = await Request("""{"NAME":"Bob"}""").ReadJsonAsync<Person>();

        Assert.Equal("Bob", person?.Name);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("")]
    [InlineData("""{"name":[1,2]}""")]
    public async Task ReadJsonAsync_BodyThatIsNotThePerson_Throws400(string body)
    {
        var error = await Assert.ThrowsAsync<RequestJsonException>(() => Request(body).ReadJsonAsync<Person>().AsTask());

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Body_IsReadOnceUnlessBuffered()
    {
        var once = Request("""{"name":"Bob"}""");
        await once.ReadJsonAsync<Person>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => once.ReadJsonAsync<Person>().AsTask());

        var buffered = Request("""{"name":"Bob"}""");
        await buffered.BufferBodyAsync();
        await buffered.ReadJsonAsync<Person>();

        Assert.Equal("Bob", (await buffered.ReadJsonAsync<Person>())?.Name);
    }
}
