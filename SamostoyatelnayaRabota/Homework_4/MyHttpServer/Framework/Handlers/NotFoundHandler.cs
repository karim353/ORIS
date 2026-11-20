using System.Net;
using MyHttpServer.Framework.Utils;

namespace MyHttpServer.Framework.Handlers;

public sealed class NotFoundHandler : Handler
{
    private readonly string _pagePath;

    public NotFoundHandler(string staticRoot)
    {
        _pagePath = Path.Combine(staticRoot, "expense-tracker", "error.html");
    }

    public override Task HandleRequestAsync(HttpListenerContext context)
    {
        string html = File.Exists(_pagePath)
            ? File.ReadAllText(_pagePath)
                .Replace("{{code}}", "404", StringComparison.Ordinal)
                .Replace("{{message}}", "Страница не найдена", StringComparison.Ordinal)
            : "404 — страница не найдена";

        return ResponseWriter.WriteHtmlAsync(context.Response, 404, html);
    }
}
