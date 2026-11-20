using System.Net;

namespace MyHttpServer.Framework;

public static class ResponseWriter
{
    public static async Task Write(HttpListenerContext context, int status, string contentType, byte[] bytes)
    {
        var response = context.Response;
        response.StatusCode = status;
        response.ContentType = contentType;
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.ContentLength64 = bytes.Length;
        if (context.Request.HttpMethod != "HEAD")
            await response.OutputStream.WriteAsync(bytes);
    }
}
