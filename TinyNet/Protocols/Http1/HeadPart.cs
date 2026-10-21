namespace TinyNet.Protocols.Http1;

internal enum HeadPart
{
    NeedMore,
    RequestLine,
    Header,
    End
}
