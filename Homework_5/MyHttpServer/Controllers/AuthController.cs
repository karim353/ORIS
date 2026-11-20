using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/auth")]
public sealed class AuthController
{
    [HttpPost("login")]
    public string Login([FromForm] string email, [FromForm] string password) => $"Login successful for {email}";

    [HttpGet("getuser")]
    public string GetUser([FromQuery] int id) => $"User with ID {id} found";
}
