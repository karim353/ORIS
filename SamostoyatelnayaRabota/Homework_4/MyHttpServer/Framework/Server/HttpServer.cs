using System.Net;
using MyHttpServer.Framework.Handlers;
using MyHttpServer.Framework.Settings;
using MyHttpServer.Framework.Utils;

namespace MyHttpServer.Framework.Server;

public sealed class HttpServer
{
    private readonly HttpListener _listener = new();
    private readonly Handler _chain;
    private volatile bool _isRunning;
    private Task? _loopTask;

    public HttpServer(AppSettings settings)
    {
        foreach (string prefix in settings.Prefixes)
            _listener.Prefixes.Add(prefix);

        string staticRoot = Path.Combine(AppContext.BaseDirectory, settings.StaticDirectory);
        var staticFiles = new StaticFilesHandler(staticRoot);
        var controllers = new ControllerHandler();
        var notFound = new NotFoundHandler(staticRoot);

        staticFiles.Successor = controllers;
        controllers.Successor = notFound;
        _chain = staticFiles;
    }

    public void Start()
    {
        _listener.Start();
        _isRunning = true;

        foreach (string prefix in _listener.Prefixes)
            Log.Info($"Сервер запущен: {prefix}");

        _loopTask = Task.Run(ListenLoopAsync);
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _listener.Stop();
        _listener.Close();

        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        Log.Info("Сервер остановлен");
    }

    private async Task ListenLoopAsync()
    {
        while (_isRunning)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_isRunning)
            {
                break;
            }
            catch (Exception exception)
            {
                Log.Error($"Ошибка при приёме запроса: {exception.Message}");
                continue;
            }

            _ = ProcessRequestAsync(context);
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        string requestName = $"{context.Request.HttpMethod} {context.Request.Url?.PathAndQuery}";
        Log.Info(requestName);

        try
        {
            await _chain.HandleRequestAsync(context);
            Log.Info($"{requestName} → {context.Response.StatusCode}");
        }
        catch (Exception exception)
        {
            Log.Error($"{requestName} → 500: {exception.Message}");
            await ResponseWriter.TryWriteErrorAsync(
                context.Response,
                500,
                "500 — внутренняя ошибка сервера");
        }
    }
}
