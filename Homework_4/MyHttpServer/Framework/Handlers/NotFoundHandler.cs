using System.Net;

namespace MyHttpServer.Framework;

public sealed class NotFoundHandler(string root) : IRequestHandler
{
    public async Task<bool> Handle(HttpListenerContext context)
    {
        var path = Path.Combine(root, "404.html");
        await ResponseWriter.Write(context, 404, "text/html; charset=utf-8", await File.ReadAllBytesAsync(path));
        return true;
    }
}
