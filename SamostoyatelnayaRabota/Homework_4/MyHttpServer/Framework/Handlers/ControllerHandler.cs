using System.Globalization;
using System.Net;
using System.Reflection;
using System.Web;
using MyHttpServer.Framework.Attributes;
using MyHttpServer.Framework.Utils;

namespace MyHttpServer.Framework.Handlers;

public sealed class ControllerHandler : Handler
{
    private readonly Dictionary<string, Type> _controllers =
        new(StringComparer.OrdinalIgnoreCase);

    public ControllerHandler()
    {
        IEnumerable<Type> controllerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => Attribute.IsDefined(type, typeof(HttpControllerAttribute)));

        foreach (Type type in controllerTypes)
        {
            string route = type.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? type.Name[..^"Controller".Length]
                : type.Name;

            _controllers[route] = type;
            Log.Info($"Контроллер {type.Name} → /{route.ToLowerInvariant()}/...");
        }
    }

    public override async Task HandleRequestAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        string[] segments = request.Url!.Segments
            .Select(segment => Uri.UnescapeDataString(segment.Trim('/')))
            .Where(segment => segment.Length > 0)
            .ToArray();

        if (segments.Length == 0 ||
            !_controllers.TryGetValue(segments[0], out Type? controllerType))
        {
            await PassToSuccessorAsync(context);
            return;
        }

        string methodName = segments.Length == 1 ? "Index" : segments[1];
        string[] routeValues = segments.Length <= 2 ? [] : segments[2..];

        MethodInfo[] candidates = controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (candidates.Length == 0)
        {
            await PassToSuccessorAsync(context);
            return;
        }

        Dictionary<string, string> namedValues = await ReadNamedValuesAsync(request);
        string effectiveMethod = request.HttpMethod;

        if (request.HttpMethod == "POST" &&
            namedValues.TryGetValue("_method", out string? methodOverride) &&
            methodOverride.Equals("PUT", StringComparison.OrdinalIgnoreCase))
            effectiveMethod = "PUT";

        Type? verbAttribute = effectiveMethod switch
        {
            "GET" => typeof(HttpGetAttribute),
            "POST" => typeof(HttpPostAttribute),
            "PUT" => typeof(HttpPutAttribute),
            _ => null
        };

        MethodInfo? method = verbAttribute is null
            ? null
            : candidates.FirstOrDefault(candidate => Attribute.IsDefined(candidate, verbAttribute));

        if (method is null)
        {
            await ResponseWriter.WriteTextAsync(response, 405,
                $"405 — метод {request.HttpMethod} не поддерживается для {request.Url.AbsolutePath}");
            return;
        }

        object?[] arguments;
        try
        {
            arguments = BindArguments(method, routeValues, namedValues);
        }
        catch (ArgumentException exception)
        {
            await ResponseWriter.WriteTextAsync(response, 400, $"400 — {exception.Message}");
            return;
        }

        object controller = Activator.CreateInstance(controllerType)
                            ?? throw new InvalidOperationException(
                                $"Не удалось создать {controllerType.Name}");

        object? result;
        try
        {
            result = method.Invoke(controller, arguments);
            result = await UnwrapTaskAsync(method, result);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            Log.Error($"Ошибка в {controllerType.Name}.{method.Name}: {exception.InnerException.Message}");
            await ResponseWriter.WriteTextAsync(response, 500, "500 — ошибка в методе контроллера");
            return;
        }

        switch (result)
        {
            case HttpResponse httpResponse:
                if (request.HttpMethod == "POST" &&
                    effectiveMethod == "PUT" &&
                    httpResponse.StatusCode == 204 &&
                    namedValues.TryGetValue("_redirect", out string? redirect) &&
                    redirect.StartsWith('/') &&
                    !redirect.StartsWith("//", StringComparison.Ordinal))
                    httpResponse = HttpResponse.Redirect(redirect);

                await ResponseWriter.WriteAsync(response, httpResponse);
                break;
            case null:
                await ResponseWriter.WriteEmptyAsync(response, 204);
                break;
            case string html:
                await ResponseWriter.WriteHtmlAsync(response, 200, html);
                break;
            default:
                await ResponseWriter.WriteJsonAsync(response, 200, result);
                break;
        }
    }

    private static async Task<Dictionary<string, string>> ReadNamedValuesAsync(
        HttpListenerRequest request)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string? key in request.QueryString.AllKeys)
        {
            if (key is not null)
                values[key] = request.QueryString[key] ?? string.Empty;
        }

        bool isForm = request.HasEntityBody &&
            (request.ContentType ?? string.Empty).StartsWith(
                "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase);

        if (!isForm)
            return values;

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var form = HttpUtility.ParseQueryString(body);

        foreach (string? key in form.AllKeys)
        {
            if (key is not null)
                values[key] = form[key] ?? string.Empty;
        }

        return values;
    }

    private static object?[] BindArguments(
        MethodInfo method,
        string[] routeValues,
        IReadOnlyDictionary<string, string> namedValues)
    {
        ParameterInfo[] parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        int nextRouteValue = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            string? raw;

            if (parameter.Name is not null &&
                namedValues.TryGetValue(parameter.Name, out string? named))
                raw = named;
            else if (nextRouteValue < routeValues.Length)
                raw = routeValues[nextRouteValue++];
            else
                raw = null;

            if (raw is null)
            {
                if (parameter.HasDefaultValue)
                {
                    arguments[i] = parameter.DefaultValue;
                    continue;
                }

                throw new ArgumentException($"не передан параметр «{parameter.Name}»");
            }

            arguments[i] = ConvertValue(raw, parameter.ParameterType, parameter.Name);
        }

        return arguments;
    }

    private static object? ConvertValue(string raw, Type targetType, string? name)
    {
        Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            if (type == typeof(string))
                return raw;
            if (type.IsEnum)
                return Enum.Parse(type, raw, true);

            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (
            exception is FormatException or InvalidCastException or
            OverflowException or ArgumentException)
        {
            throw new ArgumentException(
                $"«{raw}» не подходит для параметра «{name}» типа {type.Name}");
        }
    }

    private static async Task<object?> UnwrapTaskAsync(MethodInfo method, object? result)
    {
        if (result is not Task task)
            return result;

        await task;
        Type returnType = method.ReturnType;
        return returnType.IsGenericType &&
               returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetProperty("Result")!.GetValue(task)
            : null;
    }
}
