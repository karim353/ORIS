using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Web;

namespace MyHttpServer.Framework;

public sealed class ControllerHandler : IRequestHandler
{
    private sealed record ActionMethod(object Controller, MethodInfo Method, string Verb);
    private readonly Dictionary<string, List<ActionMethod>> routes = new(StringComparer.OrdinalIgnoreCase);

    public ControllerHandler(params object[] controllers)
    {
        foreach (var controller in controllers)
        {
            var type = controller.GetType();
            var mount = type.GetCustomAttribute<HttpControllerAttribute>()
                ?? throw new ArgumentException($"У {type.Name} отсутствует HttpController.");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<HttpActionAttribute>() is not { } action)
                    continue;
                string path = "/" + string.Join('/', new[] { mount.Path.Trim('/'), action.Path.Trim('/') }
                    .Where(segment => segment.Length > 0));
                if (!routes.TryGetValue(path, out var methods))
                    routes[path] = methods = [];
                if (methods.Any(existing => existing.Verb == action.Verb))
                    throw new InvalidOperationException($"Маршрут уже существует: {action.Verb} {path}");
                methods.Add(new ActionMethod(controller, method, action.Verb));
            }
        }
    }

    public async Task<bool> Handle(HttpListenerContext context)
    {
        var request = context.Request;
        string path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
        const string getUserPath = "/api/auth/getuser";
        string? pathId = null;
        if (path.StartsWith(getUserPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            pathId = path[(getUserPath.Length + 1)..];
            path = getUserPath;
        }
        if (!routes.TryGetValue(path.Length > 0 ? path : "/", out var methods))
            return false;
        var action = methods.Find(endpoint => endpoint.Verb == request.HttpMethod);
        if (action is null)
        {
            context.Response.Headers["Allow"] = string.Join(", ", methods.Select(endpoint => endpoint.Verb));
            await WriteError(context, 405, "Метод не поддерживается.");
            return true;
        }

        var parameters = action.Method.GetParameters();
        var values = new object?[parameters.Length];
        if (parameters.Length > 0)
        {
            var fields = request.QueryString;
            if (pathId is not null && action.Verb == "GET")
                fields = new NameValueCollection { ["id"] = pathId };
            if (action.Verb == "POST")
            {
                string? mediaType = request.ContentType?.Split(';', 2)[0].Trim();
                if (!string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteError(context, 415, "Ожидается HTML-форма.");
                    return true;
                }

                var form = await ReadForm(request);
                if (form is null)
                {
                    await WriteError(context, 413, "Форма слишком большая.");
                    return true;
                }
                fields = form;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (!TryBind(parameters[i], fields, out values[i]))
                {
                    await WriteError(context, 400, $"Проверь параметр {parameters[i].Name}.");
                    return true;
                }
            }
        }

        object? result = action.Method.Invoke(action.Controller, values);
        if (result is Task<string> pending)
            result = await pending;

        await ResponseWriter.Write(context, 200, "application/json; charset=utf-8", JsonSerializer.SerializeToUtf8Bytes(result));
        return true;
    }

    private static Task WriteError(HttpListenerContext context, int status, string message) =>
        ResponseWriter.Write(context, status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(message));

    public static bool TryBind(ParameterInfo parameter, NameValueCollection fields, out object? value)
    {
        var parts = fields.GetValues(parameter.Name!);
        value = parameter.DefaultValue;
        if (parts is null)
            return parameter.HasDefaultValue;
        if (parts.Length != 1)
            return false;
        try
        {
            var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            value = Convert.ChangeType(parts[0], type, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception error) when (error is FormatException or OverflowException or InvalidCastException)
        {
            return false;
        }
    }

    public static async Task<NameValueCollection?> ReadForm(HttpListenerRequest request)
    {
        const int limit = 16 * 1024;
        if (request.ContentLength64 > limit)
            return null;
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        int length;
        while ((length = await request.InputStream.ReadAsync(buffer)) > 0)
        {
            if (data.Length + length > limit)
                return null;
            data.Write(buffer, 0, length);
        }
        return HttpUtility.ParseQueryString(Encoding.UTF8.GetString(data.ToArray()));
    }
}
