using System.Net;
using MyHttpServer.Framework;
using MyHttpServer.ORM;
using MyHttpServer.Templating;
using Npgsql;

namespace MyHttpServer;

[HttpController("api/user")]
public sealed class UserController(NpgsqlDataSource? data) : BaseController
{
    private readonly Orm<User>? users = data is null ? null : new Orm<User>(data);

    [HttpGet("list")]
    public async Task<IHttpResult> List()
    {
        if (users is null)
            return DatabaseUnavailable();

        var records = await users.ReadAllAsync();
        var model = new { Users = records.Select(user => new { user.Name, user.Age, IsActive = false }).ToArray() };
        string path = Path.Combine(AppContext.BaseDirectory, "Templates", "User", "List.html");
        return Html(new TemplateEngine().RenderFromFile(path, model));
    }

    [HttpGet("find")]
    public async Task<IHttpResult> Find([FromQuery] int id)
    {
        if (users is null)
            return DatabaseUnavailable();

        var user = await users.ReadByIdAsync(id);
        return user is null
            ? Text($"Пользователь с Id={id} не найден", 404)
            : Text($"Найден: {user.Name}, группа: {user.GroupName}, возраст: {user.Age}");
    }

    [Authorize]
    [HttpPost("create")]
    public async Task<IHttpResult> Create([FromForm] string name, [FromForm] string groupName,
        [FromForm] int age, [FromForm] bool gender)
    {
        if (users is null)
            return DatabaseUnavailable();
        if (!Valid(name, groupName, age))
            return Text("Некорректные данные пользователя.", 400);

        await users.CreateAsync(NewUser(name, groupName, age, gender));
        return Text("Создано записей: 1");
    }

    [Authorize]
    [HttpPost("update")]
    public async Task<IHttpResult> Update([FromForm] int id, [FromForm] string name, [FromForm] string groupName,
        [FromForm] int age, [FromForm] bool gender)
    {
        if (users is null)
            return DatabaseUnavailable();
        if (!Valid(name, groupName, age))
            return Text("Некорректные данные пользователя.", 400);

        bool updated = await users.UpdateAsync(id, NewUser(name, groupName, age, gender));
        return Text($"Обновлено записей: {(updated ? 1 : 0)}");
    }

    [Authorize]
    [HttpGet("delete")]
    public async Task<IHttpResult> Delete(HttpListenerContext context, [FromQuery] int id)
    {
        var request = context.Request;
        string? origin = request.Headers["Origin"];
        if (request.Headers["Sec-Fetch-Site"] == "cross-site" ||
            (origin is not null && !string.Equals(origin, request.Url?.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase)))
            return Text("Запрос с другого сайта.", 403);
        if (users is null)
            return DatabaseUnavailable();

        bool deleted = await users.DeleteAsync(id);
        return Text($"Удалено записей: {(deleted ? 1 : 0)}");
    }

    private static bool Valid(string name, string group, int age) =>
        name.Trim().Length is > 0 and <= 80 && group.Trim().Length is > 0 and <= 80 && age is >= 0 and <= 150;

    private static User NewUser(string name, string group, int age, bool gender) =>
        new() { Name = name.Trim(), GroupName = group.Trim(), Age = age, Gender = gender };

    private static IHttpResult DatabaseUnavailable() => Text("Задай ORIS_DB для подключения к PostgreSQL.", 503);
}
