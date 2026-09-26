using System.Text.Json.Nodes;
using TinyNet.DI;
using TinyNet.Http;

namespace TinyNet.Tests;

public class RequestValuesTests
{
    public record Person(string Name);

    private static HttpContext Context(JsonObject? body = null)
    {
        var query = new Dictionary<string, string>
        {
            ["term"] = "hello",
            ["count"] = "5",
            ["broken"] = "five"
        };
        return new HttpContext(new DIContainer().CreateScope(), new HttpRequest("GET", "/", new(), query, body));
    }

    [Fact]
    public void GetFromQuery_String_ReturnsRawValue()
    {
        Assert.Equal("hello", Context().GetFromQuery("term"));
    }

    [Fact]
    public void GetFromQuery_Number_IsParsed()
    {
        Assert.Equal(5, Context().GetFromQuery<int>("count"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("broken")]
    public void GetFromQuery_MissingOrUnparsable_ReturnsNull(string name)
    {
        Assert.Null(Context().GetFromQuery<int>(name));
    }

    [Fact]
    public async Task ReadFromBodyAsync_IgnoresPropertyNameCase()
    {
        var context = Context(new JsonObject { ["name"] = "Bob" });

        var person = await context.ReadFromBodyAsync<Person>();

        Assert.Equal("Bob", person?.Name);
    }

    [Fact]
    public async Task ReadFromBodyAsync_WithoutBody_ReturnsNull()
    {
        Assert.Null(await Context().ReadFromBodyAsync<Person>());
    }

    [Fact]
    public async Task ReadFromBodyAsync_BodyOfWrongShape_ReturnsNull()
    {
        var context = Context(new JsonObject { ["Name"] = new JsonArray(1, 2) });

        Assert.Null(await context.ReadFromBodyAsync<Person>());
    }
}