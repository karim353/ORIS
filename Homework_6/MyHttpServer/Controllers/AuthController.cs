using System.Net;
using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/auth")]
public sealed class AuthController(TestMail mail) : BaseController
{
    [HttpPost("login")]
    public IHttpResult Login([FromForm] string email, [FromForm] string password) =>
        Json(new { success = true, message = $"Login successful for {email}" });

    [HttpGet("getuser")]
    public IHttpResult GetUser([FromQuery] int id) =>
        Json(new { success = true, id, message = $"User with ID {id} found" });

    [HttpGet("sendemail")]
    public async Task<IHttpResult> SendEmail(HttpListenerContext context, [FromQuery] string to) =>
        Text(await mail.SendHomework(context.Request, to));
}
