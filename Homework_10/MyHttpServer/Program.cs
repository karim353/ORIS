using MyHttpServer;
using MyHttpServer.Framework;
using Npgsql;

var settings = SettingsManager.Instance;
string? dbConnection = Environment.GetEnvironmentVariable("ORIS_DB");
await using var database = string.IsNullOrWhiteSpace(dbConnection) ? null : NpgsqlDataSource.Create(dbConnection);
var sessions = new UserSessionService();
var chain = new HandlerChain(
    new StaticFilesHandler(settings.StaticRoot),
    new ControllerHandler(sessions, new AuthController(sessions, LoginCredentials.FromEnvironment(), new TestMail()),
        new TestController(), new UserController(database), new ProfileController(), new CookieController()),
    new NotFoundHandler(settings.StaticRoot));
await using var server = new HttpServer(settings.Prefixes, chain.Handle);
server.Start();
Console.WriteLine("Введите stop для остановки.");
while (await Console.In.ReadLineAsync() is { } command)
{
    if (command.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase))
        break;
}
await server.Stop();
