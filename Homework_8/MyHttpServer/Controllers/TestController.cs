using System.Net;
using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/test")]
public sealed class TestController : BaseController
{
    [HttpGet("index")]
    public IHttpResult Index() => Html("<h1>Hello from controller!</h1>");

    [HttpGet("info")]
    public IHttpResult Info() => Json(new { id = 1, message = "Hello World!", success = true });

    [HttpGet("requestinfo")]
    public IHttpResult RequestInfo(HttpListenerContext context) => Json(new
    {
        method = context.Request.HttpMethod,
        path = context.Request.Url?.LocalPath,
        userAgent = context.Request.Headers["User-Agent"]
    });
}
