using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyHttpServer.Templating.UnitTests;

[TestClass]
public sealed class TemplateTests
{
    private readonly TemplateEngine renderer = new();

    [TestMethod]
    public void ReferenceSyntaxSupportsCollectionConditionAndVarLoop()
    {
        const string template = "$if(Users)$foreach(var user in Users)${user.Name}$endfor$else Пусто$endif";
        Assert.AreEqual("Alex", renderer.Render(template, new { Users = new[] { new { Name = "Alex" } } }));
        Assert.AreEqual(" Пусто", renderer.Render(template, new { Users = Array.Empty<object>() }));
    }

    [TestMethod]
    public void ValuesAreHtmlEncodedAndNestedPropertiesResolve()
    {
        Assert.AreEqual("<p>&lt;новая&gt;</p>",
            renderer.Render("<p>${Entry.Name}</p>", new { Entry = new { Name = "<новая>" } }));
    }

    [TestMethod]
    public void LoopsAndConditionsCanBeNested()
    {
        var model = new { Items = new[] { new { Name = "Первый", Ready = true }, new { Name = "Второй", Ready = false } } };
        string template = "$foreach(i in Items)$if(i.Ready)<b>${i.Name}</b>$else<i>${i.Name}</i>$endif$endfor";
        Assert.AreEqual("<b>Первый</b><i>Второй</i>", renderer.Render(template, model));
        Assert.AreEqual("", renderer.Render("$foreach(x in Items)${x}$endfor", new { Items = Array.Empty<int>() }));
    }

    [TestMethod]
    public void ExpressionsRespectPrecedenceAndComparisons()
    {
        string template = "$if(Ready && (Price > 100 || false))${(Price + 10) * 2}$else—$endif";
        Assert.AreEqual("260", renderer.Render(template, new { Ready = true, Price = 120 }));
        Assert.AreEqual("—", renderer.Render(template, new { Ready = true, Price = 70 }));
    }

    [TestMethod]
    public void MissingEndTagRaisesFormatError()
    {
        Assert.ThrowsExactly<FormatException>(() => renderer.Render("$if(Visible)текст", new { Visible = true }));
    }

    [TestMethod]
    public void FileRendererWritesTheResult()
    {
        var folder = Directory.CreateTempSubdirectory("oris-template-");
        try
        {
            string input = Path.Combine(folder.FullName, "source.html");
            string output = Path.Combine(folder.FullName, "result.html");
            File.WriteAllText(input, "<h1>${Name}</h1>");
            Assert.AreEqual("<h1>Тест</h1>", renderer.RenderToFile(input, output, new { Name = "Тест" }));
            Assert.AreEqual("<h1>Тест</h1>", File.ReadAllText(output));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
