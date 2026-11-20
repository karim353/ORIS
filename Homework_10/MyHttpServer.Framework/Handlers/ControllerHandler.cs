using System.Text.Json;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;

namespace MyHttpServer.Framework;

public sealed class ControllerHandler : IRequestHandler
{
    private sealed record ActionMethod(object Controller, MethodInfo Method, string Verb);
    private readonly Dictionary<string, List<ActionMethod>> routes = new(StringComparer.OrdinalIgnoreCase);

    private readonly IUserSessionService? sessions;

    public ControllerHandler(params object[] controllers) : this(null, controllers) { }

    public ControllerHandler(IUserSessionService? sessions, params object[] controllers)
    {
        this.sessions = sessions;
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
        if (!routes.TryGetValue(path.Length > 0 ? path : "/", out var methods))
            return false;
        var action = methods.Find(endpoint => endpoint.Verb == request.HttpMethod);
        if (action is null)
        {
            context.Response.Headers["Allow"] = string.Join(", ", methods.Select(endpoint => endpoint.Verb));
            await WriteError(context, 405, "Метод не поддерживается.");
            return true;
        }

        if (action.Method.IsDefined(typeof(AuthorizeAttribute), inherit: true) ||
            action.Controller.GetType().IsDefined(typeof(AuthorizeAttribute), inherit: true))
        {
            if (sessions?.GetUser(request) is null)
            {
                await WriteError(context, 401, "401 Unauthorized");
                return true;
            }
        }

        string? origin = request.Headers["Origin"];
        if (request.HttpMethod == "POST" && origin is not null &&
            !string.Equals(origin, request.Url?.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            await WriteError(context, 403, "Другой источник запроса.");
            return true;
        }

        var parameters = action.Method.GetParameters().Where(p => p.ParameterType != typeof(HttpListenerContext)).ToArray();
        var values = new Dictionary<string, object?>();
        if (parameters.Length > 0)
        {
            bool NeedsForm(ParameterInfo p) => p.IsDefined(typeof(FromFormAttribute))
                || (action.Verb == "POST" && !p.IsDefined(typeof(FromQueryAttribute)) && !p.IsDefined(typeof(FromBodyAttribute)));
            bool hasForm = parameters.Any(NeedsForm);
            bool hasJson = parameters.Any(p => p.IsDefined(typeof(FromBodyAttribute)));
            var form = new NameValueCollection();
            string? body = null;
            if (hasForm || hasJson)
            {
                string? mediaType = request.ContentType?.Split(';', 2)[0].Trim();
                string expected = hasJson ? "application/json" : "application/x-www-form-urlencoded";
                if ((hasForm && hasJson) || !string.Equals(mediaType, expected, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteError(context, 415, "Ожидается " + expected + ".");
                    return true;
                }

                body = await ReadBody(request);
                if (body is null)
                {
                    await WriteError(context, 413, "Тело запроса слишком большое.");
                    return true;
                }
                if (hasForm)
                    form = HttpUtility.ParseQueryString(body);
            }

            foreach (var parameter in parameters)
            {
                object? value;
                bool bound = parameter.IsDefined(typeof(FromBodyAttribute))
                    ? TryBindBody(parameter, body!, out value)
                    : TryBind(parameter, NeedsForm(parameter) ? form : request.QueryString, out value);
                if (!bound)
                {
                    await WriteError(context, 400, $"Проверь параметр {parameter.Name}.");
                    return true;
                }
                values.Add(parameter.Name!, value);
            }
        }

        object? result = action.Method.Invoke(action.Controller, action.Method.GetParameters()
            .Select(p => p.ParameterType == typeof(HttpListenerContext) ? context : values[p.Name!]).ToArray());
        if (result is Task<IHttpResult> pending)
            result = await pending;

        if (result is not IHttpResult httpResult)
            throw new InvalidOperationException("Контроллер должен вернуть IHttpResult.");
        await httpResult.Write(context);
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

    public static bool TryBindBody(ParameterInfo parameter, string body, out object? value)
    {
        try
        {
            value = JsonSerializer.Deserialize(body, parameter.ParameterType, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return value is not null;
        }
        catch (JsonException)
        {
            value = null;
            return false;
        }
    }

    private static async Task<string?> ReadBody(HttpListenerRequest request)
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
        return Encoding.UTF8.GetString(data.ToArray());
    }
}
