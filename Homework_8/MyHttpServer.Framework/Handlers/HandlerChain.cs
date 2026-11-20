using System.Net;

namespace MyHttpServer.Framework;

public interface IRequestHandler
{
    Task<bool> Handle(HttpListenerContext context);
}

public sealed class HandlerChain(params IRequestHandler[] handlers)
{
    public async Task Handle(HttpListenerContext context)
    {
        foreach (var handler in handlers)
        {
            if (await handler.Handle(context))
                return;
        }
    }
}
