using MyHttpServer.Framework;

var settings = SettingsManager.Instance;
await using var server = new HttpServer(settings.Prefixes, HandleRequest);
server.Start();
Console.WriteLine("Введите stop для остановки.");
while (await Console.In.ReadLineAsync() is { } command)
{
    if (command.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase))
        break;
}
await server.Stop();

async Task HandleRequest(System.Net.HttpListenerContext context)
{
    string page = Path.Combine(settings.StaticRoot, "index.html");
    await ResponseWriter.Write(context, 200, "text/html; charset=utf-8", await File.ReadAllBytesAsync(page));
}
