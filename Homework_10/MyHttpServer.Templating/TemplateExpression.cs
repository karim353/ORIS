using System.Globalization;
using System.Text.RegularExpressions;

namespace MyHttpServer.Templating;

internal sealed class TemplateExpression(string text, Func<string, object?> resolve)
{
    private static readonly Regex Tokens = new(
        """\G\s*(?:(?<path>[a-zA-Z_]\w*(?:\.[a-zA-Z_]\w*)*)|(?<number>\d+(?:\.\d+)?)|(?<text>"[^"]*"|'[^']*')|(?<symbol>&&|\|\||==|!=|<=|>=|[()!+*/%<>-]))""");
    private static readonly string[][] Operators =
    [
        ["||"], ["&&"], ["==", "!="], [">", "<", ">=", "<="], ["+", "-"], ["*", "/", "%"]
    ];
    private readonly string expression = text.Trim();
    private int position;

    internal object? Evaluate()
    {
        var value = Parse(0);
        if (position != expression.Length)
            throw new FormatException("Неожиданный символ в выражении шаблона.");
        return value;
    }

    private object? Parse(int level)
    {
        if (level == Operators.Length)
            return Atom();

        var left = Parse(level + 1);
        while (Peek() is { Success: true } token && Operators[level].Contains(token.Groups["symbol"].Value))
        {
            position += token.Length;
            left = Apply(token.Groups["symbol"].Value, left, Parse(level + 1));
        }
        return left;
    }

    private object? Atom()
    {
        var token = Peek();
        if (!token.Success)
            throw new FormatException("Ожидалось значение в шаблоне.");
        position += token.Length;

        if (token.Groups["number"].Success)
            return decimal.Parse(token.Groups["number"].Value, CultureInfo.InvariantCulture);
        if (token.Groups["text"].Success)
            return token.Groups["text"].Value[1..^1];
        if (token.Groups["path"].Success)
        {
            return token.Groups["path"].Value switch
            {
                "true" => true,
                "false" => false,
                "null" => null,
                var path => resolve(path)
            };
        }
        switch (token.Groups["symbol"].Value)
        {
            case "!": return !Flag(Atom());
            case "-": return -Number(Atom());
            case "(":
                object? value = Parse(0);
                var end = Peek();
                if (end.Groups["symbol"].Value != ")")
                    throw new FormatException("Ожидалась закрывающая скобка.");
                position += end.Length;
                return value;
            default: throw new FormatException("Неизвестное выражение.");
        }
    }

    private Match Peek() => Tokens.Match(expression, position);

    private static object Apply(string op, object? left, object? right) => op switch
    {
        "||" => Flag(left) || Flag(right),
        "&&" => Flag(left) && Flag(right),
        "==" => Equal(left, right),
        "!=" => !Equal(left, right),
        ">" => Compare(left, right) > 0,
        "<" => Compare(left, right) < 0,
        ">=" => Compare(left, right) >= 0,
        "<=" => Compare(left, right) <= 0,
        "+" when left is string || right is string => string.Concat(left, right),
        "+" => Number(left) + Number(right),
        "-" => Number(left) - Number(right),
        "*" => Number(left) * Number(right),
        "/" => Number(left) / Number(right),
        "%" => Number(left) % Number(right),
        _ => throw new FormatException("Неизвестный оператор.")
    };

    private static bool Equal(object? left, object? right) => IsNumber(left) && IsNumber(right)
        ? Number(left) == Number(right)
        : Equals(left, right);

    private static int Compare(object? left, object? right) => left is string a && right is string b
        ? string.CompareOrdinal(a, b)
        : Number(left).CompareTo(Number(right));

    private static bool Flag(object? value) => value is bool flag
        ? flag : throw new FormatException("В условии ожидалось логическое значение.");

    private static bool IsNumber(object? value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static decimal Number(object? value) => IsNumber(value)
        ? Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        : throw new FormatException("В выражении ожидалось число.");
}
