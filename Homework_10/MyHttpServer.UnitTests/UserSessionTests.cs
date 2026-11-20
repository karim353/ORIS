using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyHttpServer.Framework;

namespace MyHttpServer.UnitTests;

[TestClass]
public sealed class UserSessionTests
{
    [TestMethod]
    public void TokensAreUniqueAndExpireOrCanBeRevoked()
    {
        var clock = new ManualClock();
        var sessions = new UserSessionService(clock);
        string first = sessions.Create("one@example.test");
        string second = sessions.Create("two@example.test");
        Assert.AreNotEqual(first, second);
        Assert.AreEqual("one@example.test", sessions.Find(first));
        Assert.IsNull(sessions.Find("unknown"));
        sessions.Revoke(first);
        Assert.IsNull(sessions.Find(first));
        clock.Now += UserSessionService.Lifetime;
        Assert.IsNull(sessions.Find(second));
    }

    [TestMethod]
    public async Task AuthorizeChecksRealCookiesAndLogoutRevokesTheToken()
    {
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        string url = $"http://127.0.0.1:{port}/";
        const string email = "demo@example.test";
        const string password = "dummy-test-password";
        var sessions = new UserSessionService();
        var auth = new AuthController(sessions, new LoginCredentials(email, password), new TestMail());
        var router = new ControllerHandler(sessions, auth, new GuardedController());
        await using var server = new HttpServer([url], async context => { await router.Handle(context); });
        server.Start();

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(10) };

        using var denied = await client.GetAsync("/private/profile");
        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var invalid = await client.PostAsync("/api/auth/login", Login("bad-test-password"));
        Assert.AreEqual(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using var login = await client.PostAsync("/api/auth/login", Login(password));
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        string cookie = login.Headers.GetValues("Set-Cookie").Single();
        StringAssert.Contains(cookie, "HttpOnly");
        StringAssert.Contains(cookie, "SameSite=Strict");
        Assert.AreEqual("session active", await client.GetStringAsync("/private/profile"));

        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.AreEqual(HttpStatusCode.OK, logout.StatusCode);
        using var retry = new HttpRequestMessage(HttpMethod.Get, "/private/profile");
        retry.Headers.Add("Cookie", cookie.Split(';')[0]);
        using var afterLogout = await client.SendAsync(retry);
        Assert.AreEqual(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

        static FormUrlEncodedContent Login(string secret) =>
            new(new Dictionary<string, string> { ["email"] = email, ["password"] = secret });
    }

    [Authorize]
    [HttpController("private")]
    public sealed class GuardedController : BaseController
    {
        [HttpGet("profile")]
        public IHttpResult Profile() => Html("session active");
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
