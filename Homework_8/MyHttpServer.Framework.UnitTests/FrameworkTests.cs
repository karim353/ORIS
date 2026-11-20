using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyHttpServer.Framework.UnitTests;

[TestClass]
public sealed class FrameworkTests
{
    [TestMethod]
    public void BindsDefaultAndTypedQueryValues()
    {
        var parameter = typeof(ExampleController).GetMethod(nameof(ExampleController.Quote))!.GetParameters()[0];
        Assert.IsTrue(ControllerHandler.TryBind(parameter, new NameValueCollection(), out var fallback));
        Assert.AreEqual(4, fallback);
        Assert.IsTrue(ControllerHandler.TryBind(parameter, new NameValueCollection { ["wheels"] = "3" }, out var value));
        Assert.AreEqual(3, value);
        Assert.IsFalse(ControllerHandler.TryBind(parameter, new NameValueCollection { ["wheels"] = "три" }, out _));
    }

    [TestMethod]
    public void FindsStaticIndexAndMimeType()
    {
        var folder = Directory.CreateTempSubdirectory("oris-static-");
        try
        {
            var index = Path.Combine(folder.FullName, "index.html");
            File.WriteAllText(index, "<h1>Тест</h1>");
            var files = new StaticFiles(folder.FullName);
            Assert.AreEqual(index, files.FindFile("/"));
            Assert.IsNull(files.FindFile("/missing.css"));
            Assert.AreEqual("text/css; charset=utf-8", StaticFiles.ContentType("site.CSS"));
            Assert.AreEqual("application/octet-stream", StaticFiles.ContentType("file.unknown"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task ListenerRoutesRequestsAndWritesHtmlAndJson()
    {
        var folder = Directory.CreateTempSubdirectory("oris-listener-");
        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "404.html"), "Страница не найдена");
            File.WriteAllText(Path.Combine(folder.FullName, "index.html"), "Статика");
            int port;
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
            }

            string url = $"http://127.0.0.1:{port}/";
            var chain = new HandlerChain(new StaticFilesHandler(folder.FullName),
                new ControllerHandler(new ExampleController()), new NotFoundHandler(folder.FullName));
            await using var server = new HttpServer([url], chain.Handle);
            server.Start();
            using var client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(10) };
            Assert.AreEqual("Статика", await client.GetStringAsync("/"));
            Assert.AreEqual("{\"wheels\":3}", await client.GetStringAsync("/test/quote?wheels=3"));

            using var bad = await client.GetAsync("/test/quote?wheels=oops");
            Assert.AreEqual(HttpStatusCode.BadRequest, bad.StatusCode);
            using var missing = await client.GetAsync("/missing");
            Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.AreEqual("Страница не найдена", await missing.Content.ReadAsStringAsync());
            using var wrongMethod = await client.PostAsync("/test/quote", null);
            Assert.AreEqual(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);

            using var body = new StringContent("{\"wheels\":6}", Encoding.UTF8, "application/json");
            using var result = await client.PostAsync("/test/echo?express=true", body);
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.AreEqual("text/html", result.Content.Headers.ContentType?.MediaType);
            Assert.AreEqual("<p>6 True</p>", await result.Content.ReadAsStringAsync());
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [HttpController("test")]
    public sealed class ExampleController : BaseController
    {
        [HttpGet("quote")]
        public IHttpResult Quote([FromQuery] int wheels = 4) => Json(new { wheels });

        [HttpPost("echo")]
        public IHttpResult Echo([FromBody] Payload data, [FromQuery] bool express = false) =>
            Html($"<p>{data.Wheels} {express}</p>");
    }

    public sealed record Payload(int Wheels);
}
