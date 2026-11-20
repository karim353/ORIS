using MyHttpServer.Framework;
using MyHttpServer.Templating;

namespace MyHttpServer;

[HttpController("api/user")]
public sealed class DemoUsersController : BaseController
{
    private sealed record DemoUser(string Name, int Age, string Email, bool IsActive);

    private static readonly DemoUser[] Users =
    [
        new("Alex", 20, "alex@example.com", true),
        new("Maria", 22, "maria@example.com", false),
        new("Ivan", 19, "ivan@example.com", true)
    ];

    [HttpGet("profile")]
    public IHttpResult Profile() => Render("Profile.html", Users[0]);

    [HttpGet("list")]
    public IHttpResult List() => Render("List.html", new { Users });

    private static IHttpResult Render(string name, object model)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Templates", "User", name);
        return Html(new TemplateEngine().RenderFromFile(path, model));
    }
}
