using System.Net;
using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/auth")]
public sealed class AuthController(UserSessionService sessions, LoginCredentials? credentials, TestMail mail) : BaseController
{
    private static readonly LoginCredentials DemoCredential = new("student", "test123");

    [HttpPost("login")]
    public IHttpResult Login(HttpListenerContext context, [FromForm] string email, [FromForm] string password)
    {
        var request = context.Request;
        bool local = request.LocalEndPoint is { } listener && IPAddress.IsLoopback(listener.Address) &&
            request.RemoteEndPoint is { } client && IPAddress.IsLoopback(client.Address);
        var account = credentials ?? (local ? DemoCredential : null);
        if (account?.Matches(email, password) != true)
            return Json(new { Success = false, Message = "Неверный логин или пароль" }, 401);

        sessions.Revoke(UserSessionService.ReadToken(request));
        UserSessionService.WriteCookie(context, sessions.Create(email.Trim()));
        return Json(new { Success = true, message = "Вход выполнен" });
    }

    [HttpPost("logout")]
    public IHttpResult Logout(HttpListenerContext context)
    {
        sessions.Revoke(UserSessionService.ReadToken(context.Request));
        UserSessionService.WriteCookie(context, null);
        return Json(new { Success = true, Message = "Выход выполнен" });
    }

    [HttpGet("getuser")]
    public IHttpResult GetUser([FromQuery] int id) =>
        Json(new { success = true, id, message = $"User with ID {id} found" });

    [HttpGet("sendemail")]
    public async Task<IHttpResult> SendEmail(HttpListenerContext context, [FromQuery] string to) =>
        Text(await mail.SendHomework(context.Request, to));
}
