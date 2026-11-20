using System.Globalization;
using System.Net;
using MyHttpServer.Framework.Attributes;
using MyHttpServer.Framework.Settings;
using MyHttpServer.Framework.Utils;

namespace MyHttpServer.Controllers;

[HttpController]
public sealed class ExpensesController
{
    private static readonly ExpenseService Expenses = new();

    [HttpGet]
    public HttpResponse Index(string? category = null)
    {
        try
        {
            category = Normalize(category);
            return HttpResponse.Html(200, ExpensePages.List(
                Expenses.GetAll(category),
                Expenses.GetApprovedTotals(),
                category));
        }
        catch (ArgumentException exception)
        {
            return HttpResponse.Html(400, ExpensePages.Error(400, exception.Message));
        }
    }

    [HttpGet]
    public HttpResponse Details(int id)
    {
        if (id <= 0)
            return HttpResponse.Html(400,
                ExpensePages.Error(400, "Параметр id должен быть положительным целым числом."));

        Expense? expense = Expenses.GetById(id);
        return expense is null
            ? HttpResponse.Html(404, ExpensePages.Error(404, "Расход не найден."))
            : HttpResponse.Html(200, ExpensePages.Details(expense));
    }

    [HttpPost]
    public HttpResponse Create(string category, int amount)
    {
        try
        {
            Expenses.Create(category, amount);
            return HttpResponse.Redirect("/expenses");
        }
        catch (ArgumentException exception)
        {
            return HttpResponse.Html(400, ExpensePages.Error(400, exception.Message));
        }
    }

    [HttpPut]
    public HttpResponse Update(int id, string category, int amount, string status)
    {
        if (id <= 0)
            return HttpResponse.Html(400,
                ExpensePages.Error(400, "Параметр id должен быть положительным целым числом."));

        try
        {
            return Expenses.Update(id, category, amount, status)
                ? HttpResponse.NoContent()
                : HttpResponse.Html(404, ExpensePages.Error(404, "Расход не найден."));
        }
        catch (ArgumentException exception)
        {
            return HttpResponse.Html(400, ExpensePages.Error(400, exception.Message));
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

public sealed record Expense(int Id, string Category, int Amount, string Status);

public static class ExpenseCategories
{
    public const string Food = "food";
    public const string Transport = "transport";
    public const string Other = "other";

    public static readonly string[] All = [Food, Transport, Other];
    public static bool IsValid(string? value) => value is Food or Transport or Other;
}

public static class ExpenseStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";

    public static bool IsValid(string? value) => value is Pending or Approved;
}

public sealed class ExpenseService
{
    private readonly object _sync = new();
    private readonly List<Expense> _expenses = [];
    private int _nextId = 1;

    public IReadOnlyList<Expense> GetAll(string? category = null)
    {
        category = Normalize(category);
        if (category is not null && !ExpenseCategories.IsValid(category))
            throw new ArgumentException("Категория должна быть food, transport или other.", nameof(category));

        lock (_sync)
        {
            return _expenses
                .Where(expense => category is null || expense.Category == category)
                .OrderBy(expense => expense.Id)
                .ToArray();
        }
    }

    public Expense? GetById(int id)
    {
        lock (_sync)
            return _expenses.FirstOrDefault(expense => expense.Id == id);
    }

    public Expense Create(string? category, int amount)
    {
        category = Normalize(category);
        Validate(category, amount, ExpenseStatuses.Pending);

        lock (_sync)
        {
            var expense = new Expense(_nextId++, category!, amount, ExpenseStatuses.Pending);
            _expenses.Add(expense);
            return expense;
        }
    }

    public bool Update(int id, string? category, int amount, string? status)
    {
        category = Normalize(category);
        status = Normalize(status);
        Validate(category, amount, status);

        lock (_sync)
        {
            int index = _expenses.FindIndex(expense => expense.Id == id);
            if (index < 0)
                return false;

            _expenses[index] = new Expense(id, category!, amount, status!);
            return true;
        }
    }

    public IReadOnlyDictionary<string, int> GetApprovedTotals()
    {
        lock (_sync)
        {
            return ExpenseCategories.All.ToDictionary(
                category => category,
                category => _expenses
                    .Where(expense => expense.Category == category &&
                                      expense.Status == ExpenseStatuses.Approved)
                    .Sum(expense => expense.Amount));
        }
    }

    private static void Validate(string? category, int amount, string? status)
    {
        if (!ExpenseCategories.IsValid(category))
            throw new ArgumentException("Категория должна быть food, transport или other.", nameof(category));
        if (amount is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Сумма должна быть целым числом от 1 до 100000.");
        if (!ExpenseStatuses.IsValid(status))
            throw new ArgumentException("Статус должен быть pending или approved.", nameof(status));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

internal static class ExpensePages
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly IReadOnlyDictionary<string, string> EmptyValues =
        new Dictionary<string, string>();

    public static string List(
        IReadOnlyList<Expense> expenses,
        IReadOnlyDictionary<string, int> totals,
        string? selectedCategory)
    {
        string page = Read("index.html");
        page = Repeat(page, "expenses", expenses.Select(ExpenseValues));
        page = Repeat(page, "categories", ExpenseCategories.All.Select(category =>
            CategoryValues(category, selectedCategory)));
        page = Repeat(page, "empty", expenses.Count == 0
            ? new[] { EmptyValues }
            : Array.Empty<IReadOnlyDictionary<string, string>>());
        page = Repeat(page, "reset", selectedCategory is null
            ? Array.Empty<IReadOnlyDictionary<string, string>>()
            : new[] { EmptyValues });

        return Fill(page, new Dictionary<string, string>
        {
            ["food_total"] = FormatAmount(totals[ExpenseCategories.Food]),
            ["transport_total"] = FormatAmount(totals[ExpenseCategories.Transport]),
            ["other_total"] = FormatAmount(totals[ExpenseCategories.Other])
        });
    }

    public static string Details(Expense expense)
    {
        string page = Repeat(
            Read("details.html"),
            "categories",
            ExpenseCategories.All.Select(category => CategoryValues(category, expense.Category)));

        return Fill(page, new Dictionary<string, string>
        {
            ["expense_id"] = expense.Id.ToString(CultureInfo.InvariantCulture),
            ["category_name"] = Encode(CategoryName(expense.Category)),
            ["amount"] = FormatAmount(expense.Amount),
            ["amount_value"] = expense.Amount.ToString(CultureInfo.InvariantCulture),
            ["status"] = Encode(expense.Status),
            ["status_name"] = Encode(StatusName(expense.Status)),
            ["pending_selected"] = expense.Status == ExpenseStatuses.Pending ? " selected" : string.Empty,
            ["approved_selected"] = expense.Status == ExpenseStatuses.Approved ? " selected" : string.Empty
        });
    }

    public static string Error(int code, string message) =>
        Fill(Read("error.html"), new Dictionary<string, string>
        {
            ["code"] = code.ToString(CultureInfo.InvariantCulture),
            ["message"] = Encode(message)
        });

    private static IReadOnlyDictionary<string, string> ExpenseValues(Expense expense) =>
        new Dictionary<string, string>
        {
            ["expense_id"] = expense.Id.ToString(CultureInfo.InvariantCulture),
            ["category_name"] = Encode(CategoryName(expense.Category)),
            ["amount"] = FormatAmount(expense.Amount),
            ["status"] = Encode(expense.Status),
            ["status_name"] = Encode(StatusName(expense.Status))
        };

    private static IReadOnlyDictionary<string, string> CategoryValues(
        string category,
        string? selectedCategory) =>
        new Dictionary<string, string>
        {
            ["category"] = Encode(category),
            ["category_name"] = Encode(CategoryName(category)),
            ["selected"] = category == selectedCategory ? " selected" : string.Empty
        };

    private static string Read(string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            SettingsManager.Instance.Settings.StaticDirectory,
            "expense-tracker",
            fileName);

        return File.ReadAllText(path);
    }

    private static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        foreach ((string key, string value) in values)
            template = template.Replace("{{" + key + "}}", value, StringComparison.Ordinal);

        return template;
    }

    private static string Repeat(
        string template,
        string blockName,
        IEnumerable<IReadOnlyDictionary<string, string>> items)
    {
        string opening = "{{#" + blockName + "}}";
        string closing = "{{/" + blockName + "}}";
        int start = template.IndexOf(opening, StringComparison.Ordinal);
        int end = template.IndexOf(closing, StringComparison.Ordinal);

        if (start < 0 || end < start)
            return template;

        int contentStart = start + opening.Length;
        string block = template[contentStart..end];
        string content = string.Concat(items.Select(values => Fill(block, values)));
        return template[..start] + content + template[(end + closing.Length)..];
    }

    private static string FormatAmount(int amount) => amount.ToString("N0", Russian);
    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string CategoryName(string category) => category switch
    {
        ExpenseCategories.Food => "Продукты",
        ExpenseCategories.Transport => "Транспорт",
        ExpenseCategories.Other => "Прочее",
        _ => category
    };

    private static string StatusName(string status) => status == ExpenseStatuses.Approved
        ? "Подтверждён"
        : "Ожидает";
}
