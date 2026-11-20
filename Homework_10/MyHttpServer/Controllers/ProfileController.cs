using System.Net;
using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/user")]
public sealed class ProfileController : BaseController
{
    [Authorize]
    [HttpGet("profile")]
    public IHttpResult Profile(HttpListenerContext context)
    {
        context.Response.Headers["Cache-Control"] = "no-store";
        return Html("<h1>Личный кабинет</h1><p>Вы успешно авторизованы.</p>");
    }
}
