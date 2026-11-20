using System.Net;
using System.Text;
using System.Text.Json;

namespace MyHttpServer.Framework;

public interface IHttpResult
{
    Task Write(HttpListenerContext context);
}

public sealed record HtmlResult(string Content, int Status = 200) : IHttpResult
{
    public Task Write(HttpListenerContext context) =>
        ResponseWriter.Write(context, Status, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Content));
}

public sealed record JsonResult(object? Value, int Status = 200) : IHttpResult
{
    public Task Write(HttpListenerContext context) =>
        ResponseWriter.Write(context, Status, "application/json; charset=utf-8", JsonSerializer.SerializeToUtf8Bytes(Value));
}

public sealed record TextResult(string Content, int Status = 200) : IHttpResult
{
    public Task Write(HttpListenerContext context) =>
        ResponseWriter.Write(context, Status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(Content));
}

public abstract class BaseController
{
    protected static TextResult Text(string content, int status = 200) => new(content, status);
    protected static HtmlResult Html(string content, int status = 200) => new(content, status);
    protected static JsonResult Json(object? value, int status = 200) => new(value, status);
}
