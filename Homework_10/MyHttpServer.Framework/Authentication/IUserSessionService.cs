using System.Net;

namespace MyHttpServer.Framework;

public interface IUserSessionService
{
    string? GetUser(HttpListenerRequest request);
}
