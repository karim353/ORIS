using System.Net;
using System.Text;

namespace MyHttpServer.Framework;

public sealed class HttpServer(IEnumerable<string> prefixes, Func<HttpListenerContext, Task> handle) : IAsyncDisposable
{
    private readonly HttpListener listener = new();
    private Task? loop;

    public void Start()
    {
        foreach (string prefix in prefixes)
            listener.Prefixes.Add(prefix);
        listener.Start();
        Console.WriteLine("Сервер запущен: " + string.Join(", ", listener.Prefixes));
        loop = Listen();
    }

    public async Task Stop()
    {
        if (loop is null)
            return;

        listener.Stop();
        await loop;
        loop = null;
        Console.WriteLine("Сервер остановлен");
    }

    private async Task Listen()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException) when (!listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            Console.WriteLine($"{context.Request.HttpMethod} {context.Request.Url?.AbsolutePath}");
            try
            {
                await handle(context);
            }
            catch (Exception error) when (error is IOException or HttpListenerException or ObjectDisposedException)
            {
                Console.Error.WriteLine(error.Message);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.GetType().Name);
                try
                {
                    await ResponseWriter.Write(context, 500, "text/plain; charset=utf-8",
                        Encoding.UTF8.GetBytes("Ошибка сервера"));
                }
                catch (Exception writeError) when (writeError is IOException or HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    Console.Error.WriteLine(writeError.GetType().Name);
                }
            }
            finally
            {
                context.Response.Close();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Stop();
        listener.Close();
    }
}
