namespace MyHttpServer.Framework;

[AttributeUsage(AttributeTargets.Class)]
public sealed class HttpControllerAttribute(string path) : Attribute
{
    public string Path { get; } = path;
}

[AttributeUsage(AttributeTargets.Method)]
public abstract class HttpActionAttribute(string verb, string path) : Attribute
{
    public string Verb { get; } = verb;
    public string Path { get; } = path;
}

public sealed class HttpGetAttribute(string path) : HttpActionAttribute("GET", path);
public sealed class HttpPostAttribute(string path) : HttpActionAttribute("POST", path);
