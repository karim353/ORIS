using System.Net;
using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/cookie")]
public sealed class CookieController : BaseController
{
    private const string CookieName = "testCookie";

    [HttpGet("set")]
    public IHttpResult Set(HttpListenerContext context)
    {
        WriteCookie(context, "ValueOfTestCookie", TimeSpan.FromMinutes(30));
        return Html("<h1>Cookie установлена</h1>");
    }

    [HttpGet("get")]
    public IHttpResult Get(HttpListenerContext context)
    {
        context.Response.Headers["Cache-Control"] = "no-store";
        return Json(new { Value = context.Request.Cookies[CookieName]?.Value });
    }

    [HttpGet("delete")]
    public IHttpResult Delete(HttpListenerContext context)
    {
        WriteCookie(context, "", TimeSpan.Zero);
        return Html("<h1>Cookie удалена</h1>");
    }

    private static void WriteCookie(HttpListenerContext context, string value, TimeSpan lifetime)
    {
        string secure = context.Request.IsSecureConnection ? "; Secure" : "";
        context.Response.AppendHeader("Set-Cookie",
            $"{CookieName}={value}; Path=/; HttpOnly; SameSite=Strict; Max-Age={(int)lifetime.TotalSeconds}{secure}");
        context.Response.Headers["Cache-Control"] = "no-store";
    }
}
