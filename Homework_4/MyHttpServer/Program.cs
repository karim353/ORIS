using MyHttpServer;
using MyHttpServer.Framework;

var settings = SettingsManager.Instance;
var chain = new HandlerChain(new StaticFilesHandler(settings.StaticRoot), new ControllerHandler(new AuthController()), new NotFoundHandler(settings.StaticRoot));
await using var server = new HttpServer(settings.Prefixes, chain.Handle);
server.Start();
Console.WriteLine("Введите stop для остановки.");
while (await Console.In.ReadLineAsync() is { } command)
{
    if (command.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase))
        break;
}
await server.Stop();
