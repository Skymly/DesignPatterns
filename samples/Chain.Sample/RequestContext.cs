namespace Chain.Sample;

public sealed class RequestContext
{
    public RequestContext(string path, bool isAuthenticated)
    {
        Path = path;
        IsAuthenticated = isAuthenticated;
    }

    public string Path { get; }

    public bool IsAuthenticated { get; }

    public string? Response { get; set; }
}
