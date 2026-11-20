using System.Net;

namespace MyHttpServer.Framework;

public sealed class StaticFilesHandler(string root) : IRequestHandler
{
    private readonly StaticFiles files = new(root);

    public async Task<bool> Handle(HttpListenerContext context)
    {
        if (context.Request.HttpMethod is not ("GET" or "HEAD"))
            return false;

        string path = context.Request.Url?.AbsolutePath ?? "/";
        string? file = files.FindFile(path);
        if (file is null)
            return false;

        if (!path.EndsWith('/') && Directory.Exists(Path.Combine(root, Uri.UnescapeDataString(path).TrimStart('/'))))
        {
            context.Response.RedirectLocation = path + "/" + context.Request.Url?.Query;
            await ResponseWriter.Write(context, 308, "text/plain; charset=utf-8", []);
        }
        else
        {
            await ResponseWriter.Write(context, 200, StaticFiles.ContentType(file), await File.ReadAllBytesAsync(file));
        }
        return true;
    }
}
