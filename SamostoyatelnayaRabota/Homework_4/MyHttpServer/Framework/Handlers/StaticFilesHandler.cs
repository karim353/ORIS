using System.Net;
using MyHttpServer.Framework.Utils;

namespace MyHttpServer.Framework.Handlers;

public sealed class StaticFilesHandler : Handler
{
    private readonly string _root;

    public StaticFilesHandler(string staticRoot)
    {
        _root = Path.GetFullPath(staticRoot);
    }

    public override async Task HandleRequestAsync(HttpListenerContext context)
    {
        if (context.Request.HttpMethod != "GET")
        {
            await PassToSuccessorAsync(context);
            return;
        }

        string urlPath = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath);
        string? fullPath = ToSafePath(urlPath);

        if (fullPath is null)
        {
            await PassToSuccessorAsync(context);
            return;
        }

        if (Directory.Exists(fullPath))
        {
            if (!urlPath.EndsWith('/'))
            {
                ResponseWriter.Redirect(context.Response, urlPath + "/");
                return;
            }

            fullPath = Path.Combine(fullPath, "index.html");
        }

        if (!File.Exists(fullPath))
        {
            await PassToSuccessorAsync(context);
            return;
        }

        byte[] bytes = await File.ReadAllBytesAsync(fullPath);
        await ResponseWriter.WriteBytesAsync(context.Response, 200, bytes, MimeTypes.Get(fullPath));
    }

    private string? ToSafePath(string urlPath)
    {
        string relative = urlPath.TrimStart('/');
        string fullPath = Path.GetFullPath(Path.Combine(_root, relative));

        if (fullPath.Equals(_root, StringComparison.OrdinalIgnoreCase))
            return fullPath;

        return fullPath.StartsWith(
            _root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }
}
