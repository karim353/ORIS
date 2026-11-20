using System.Net;
using MyHttpServer.Framework.Utils;

namespace MyHttpServer.Framework.Handlers;

public abstract class Handler
{
    public Handler? Successor { get; set; }

    public abstract Task HandleRequestAsync(HttpListenerContext context);

    protected Task PassToSuccessorAsync(HttpListenerContext context) =>
        Successor is not null
            ? Successor.HandleRequestAsync(context)
            : ResponseWriter.WriteTextAsync(context.Response, 404, "Not Found");
}
