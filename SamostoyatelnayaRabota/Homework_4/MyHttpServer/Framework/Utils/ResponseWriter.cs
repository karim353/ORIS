using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace MyHttpServer.Framework.Utils;

public static class ResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static async Task WriteBytesAsync(
        HttpListenerResponse response,
        int statusCode,
        byte[] body,
        string contentType)
    {
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = body.Length;
        if (body.Length > 0)
            await response.OutputStream.WriteAsync(body);
        response.Close();
    }

    public static Task WriteTextAsync(
        HttpListenerResponse response,
        int statusCode,
        string text,
        string contentType = "text/plain; charset=utf-8") =>
        WriteBytesAsync(response, statusCode, Encoding.UTF8.GetBytes(text), contentType);

    public static Task WriteHtmlAsync(HttpListenerResponse response, int statusCode, string html) =>
        WriteTextAsync(response, statusCode, html, "text/html; charset=utf-8");

    public static Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object? data) =>
        WriteTextAsync(response, statusCode, JsonSerializer.Serialize(data, JsonOptions),
            "application/json; charset=utf-8");

    public static Task WriteEmptyAsync(HttpListenerResponse response, int statusCode) =>
        WriteBytesAsync(response, statusCode, [], "text/plain; charset=utf-8");

    public static Task WriteAsync(HttpListenerResponse response, HttpResponse result)
    {
        if (result.RedirectLocation is not null)
            response.RedirectLocation = result.RedirectLocation;

        return result.Body is null
            ? WriteEmptyAsync(response, result.StatusCode)
            : WriteTextAsync(response, result.StatusCode, result.Body, result.ContentType);
    }

    public static void Redirect(HttpListenerResponse response, string location)
    {
        response.StatusCode = 301;
        response.RedirectLocation = location;
        response.Close();
    }

    public static async Task TryWriteErrorAsync(
        HttpListenerResponse response,
        int statusCode,
        string message)
    {
        try
        {
            await WriteTextAsync(response, statusCode, message);
        }
        catch
        {
            try { response.Abort(); } catch { }
        }
    }
}

public sealed record HttpResponse(
    int StatusCode,
    string? Body = null,
    string ContentType = "text/plain; charset=utf-8",
    string? RedirectLocation = null)
{
    public static HttpResponse Html(int statusCode, string html) =>
        new(statusCode, html, "text/html; charset=utf-8");

    public static HttpResponse Redirect(string location) =>
        new(303, RedirectLocation: location);

    public static HttpResponse NoContent() => new(204);
}
