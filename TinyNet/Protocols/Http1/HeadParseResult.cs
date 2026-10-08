namespace TinyNet.Protocols.Http1;

internal enum HeadParseStatus
{
    Incomplete,
    Complete,
    Rejected
}

internal readonly record struct HeadParseResult(
    HeadParseStatus Status,
    RequestHead? Head = null,
    int StatusCode = 0,
    string? Reason = null)
{
    public static HeadParseResult Incomplete => new(HeadParseStatus.Incomplete);
    public static HeadParseResult Complete(RequestHead head) => new(HeadParseStatus.Complete, head);
    public static HeadParseResult Rejected(int statusCode, string reason) => new(HeadParseStatus.Rejected, null, statusCode, reason);
}