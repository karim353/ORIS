using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MyHttpServer.Templating;

public sealed class TemplateEngine
{
    private const string PathPattern = @"[a-zA-Z_]\w*(?:\.[a-zA-Z_]\w*)*";
    private static readonly Regex Tokens = new(
        @"\${(?<value>[^}]+)\}|" +
        @"\$if\s*\(\s*(?<condition>(?:[^()]|\((?<depth>)|\)(?<-depth>))*)(?(depth)(?!))\s*\)|" +
        @"\$foreach\s*\(\s*(?:var\s+)?(?<item>[a-zA-Z_]\w*)\s+in\s+(?<source>" + PathPattern + @")\s*\)|" +
        @"\$(?<end>else|endif|endforeach|endfor)\b",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private abstract record Node;
    private sealed record Literal(string Text) : Node;
    private sealed record Value(string Path) : Node;
    private sealed record Conditional(string Path, List<Node> Yes, List<Node> No) : Node;
    private sealed record Loop(string Name, string Path, List<Node> Body) : Node;

    public string Render(string template, object model)
    {
        var parser = new Parser(template);
        var nodes = parser.Parse(0, out string? end);
        if (end is not null)
            throw new FormatException($"Неожиданный маркер шаблона: {end}.");
        var output = new StringBuilder();
        Write(nodes, model, new Dictionary<string, object?>(StringComparer.Ordinal), output);
        return output.ToString();
    }

    public string RenderFromFile(string path, object model) => Render(File.ReadAllText(path), model);

    public string RenderToFile(string input, string output, object model)
    {
        string result = RenderFromFile(input, model);
        File.WriteAllText(output, result);
        return result;
    }

    private sealed class Parser(string template)
    {
        private Match _token = Tokens.Match(template);
        private int _position;

        public List<Node> Parse(int depth, out string? end)
        {
            if (depth > 24)
                throw new FormatException("Слишком глубокая вложенность шаблона.");
            var nodes = new List<Node>();
            end = null;
            while (_token.Success)
            {
                Match token = _token;
                nodes.Add(new Literal(template[_position..token.Index]));
                _position = token.Index + token.Length;
                _token = token.NextMatch();
                if (token.Groups["end"].Success)
                {
                    end = token.Groups["end"].Value;
                    return nodes;
                }
                if (token.Groups["value"].Success)
                {
                    nodes.Add(new Value(token.Groups["value"].Value));
                    continue;
                }
                List<Node> body = Parse(depth + 1, out string? marker);
                if (token.Groups["condition"].Success)
                {
                    List<Node> otherwise = marker == "else" ? Parse(depth + 1, out marker) : [];
                    Require(marker, "endif");
                    nodes.Add(new Conditional(token.Groups["condition"].Value, body, otherwise));
                }
                else
                {
                    Require(marker == "endfor" ? "endforeach" : marker, "endforeach");
                    nodes.Add(new Loop(token.Groups["item"].Value, token.Groups["source"].Value, body));
                }
            }
            nodes.Add(new Literal(template[_position..]));
            return nodes;
        }

        private static void Require(string? actual, string expected)
        {
            if (actual != expected)
                throw new FormatException($"Ожидался маркер шаблона {expected}.");
        }
    }

    private static void Write(List<Node> nodes, object model, Dictionary<string, object?> locals, StringBuilder output)
    {
        foreach (Node node in nodes)
        {
            switch (node)
            {
                case Literal literal:
                    output.Append(literal.Text);
                    break;
                case Value value:
                    object? resolved = new TemplateExpression(value.Path, path => Resolve(path, model, locals)).Evaluate();
                    output.Append(WebUtility.HtmlEncode(Convert.ToString(resolved, CultureInfo.InvariantCulture)));
                    break;
                case Conditional condition:
                    object? conditionValue = new TemplateExpression(condition.Path, path => Resolve(path, model, locals)).Evaluate();
                    bool yes = conditionValue switch
                    {
                        bool flag => flag,
                        null => false,
                        IEnumerable sequence when conditionValue is not string => sequence.Cast<object?>().Any(),
                        _ => throw new FormatException($"Условие {condition.Path} должно быть bool или коллекцией.")
                    };
                    Write(yes ? condition.Yes : condition.No, model, locals, output);
                    break;
                case Loop loop:
                    if (Resolve(loop.Path, model, locals) is not IEnumerable items || items is string)
                        throw new FormatException($"{loop.Path} не является коллекцией.");
                    var nested = new Dictionary<string, object?>(locals, StringComparer.Ordinal);
                    foreach (object? item in items)
                    {
                        nested[loop.Name] = item;
                        Write(loop.Body, model, nested, output);
                    }
                    break;
            }
        }
    }

    private static object? Resolve(string path, object model, Dictionary<string, object?> locals)
    {
        string[] parts = path.Split('.');
        int start = 0;
        if (locals.TryGetValue(parts[0], out object? value))
            start = 1;
        else
            value = model;
        foreach (string part in parts.Skip(start))
        {
            if (value is null)
                return null;
            var property = value.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
            if (property is null || property.GetIndexParameters().Length != 0)
                throw new FormatException($"Неизвестное поле шаблона: {path}.");
            value = property.GetValue(value);
        }
        return value;
    }
}
