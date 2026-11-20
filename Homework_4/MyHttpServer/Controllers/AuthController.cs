using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/auth")]
public sealed class AuthController
{
    [HttpPost("login")]
    public string Login(string email, string password) => $"Login successful for {email}";

    [HttpGet("getuser")]
    public string GetUser(int id) => $"User with ID {id} found";
}
