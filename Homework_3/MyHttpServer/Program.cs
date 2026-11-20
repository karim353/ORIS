using MyHttpServer.Framework;

var settings = SettingsManager.Instance;
var files = new StaticFiles(settings.StaticRoot);
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
    if (context.Request.HttpMethod is not ("GET" or "HEAD"))
    {
        context.Response.Headers["Allow"] = "GET, HEAD";
        await ResponseWriter.Write(context, 405, "text/plain; charset=utf-8", []);
        return;
    }
    var path = context.Request.Url?.AbsolutePath ?? "/";
    string? file = files.FindFile(path);
    if (file is not null && !path.EndsWith('/') && Directory.Exists(Path.Combine(settings.StaticRoot, path.TrimStart('/'))))
    {
        context.Response.RedirectLocation = path + "/";
        await ResponseWriter.Write(context, 308, "text/plain; charset=utf-8", []);
        return;
    }
    int status = file is null ? 404 : 200;
    file ??= Path.Combine(settings.StaticRoot, "404.html");
    await ResponseWriter.Write(context, status, StaticFiles.ContentType(file), await File.ReadAllBytesAsync(file));
}
