using System.Net;
using System.Security.Cryptography;
using MyHttpServer.Framework;

namespace MyHttpServer;

public sealed class UserSessionService(TimeProvider? timeProvider = null) : IUserSessionService
{
    public const string CookieName = "sessionId";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private sealed record Session(string User, DateTimeOffset Expires);
    private readonly Dictionary<string, Session> sessions = new(StringComparer.Ordinal);
    private readonly object sync = new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public string Create(string user)
    {
        lock (sync)
        {
            DateTimeOffset now = clock.GetUtcNow();
            foreach (var expired in sessions.Where(pair => pair.Value.Expires <= now).Select(pair => pair.Key).ToArray())
                sessions.Remove(expired);
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            sessions.Add(token, new(user, now.Add(Lifetime)));
            return token;
        }
    }

    public string? Find(string? token)
    {
        if (token is null)
            return null;
        lock (sync)
        {
            if (!sessions.TryGetValue(token, out Session? session))
                return null;
            if (session.Expires > clock.GetUtcNow())
                return session.User;
            sessions.Remove(token);
            return null;
        }
    }

    public void Revoke(string? token)
    {
        if (token is null)
            return;
        lock (sync)
            sessions.Remove(token);
    }

    public string? GetUser(HttpListenerRequest request) => Find(ReadToken(request));

    public static string? ReadToken(HttpListenerRequest request) => request.Cookies[CookieName]?.Value;

    public static void WriteCookie(HttpListenerContext context, string? token)
    {
        int age = token is null ? 0 : (int)Lifetime.TotalSeconds;
        string secure = context.Request.IsSecureConnection ? "; Secure" : "";
        context.Response.AppendHeader("Set-Cookie",
            $"{CookieName}={token}; Path=/; HttpOnly; SameSite=Strict; Max-Age={age}{secure}");
        context.Response.Headers["Cache-Control"] = "no-store";
    }

}
