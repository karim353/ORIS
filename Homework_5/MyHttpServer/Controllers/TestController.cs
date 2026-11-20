using System.Net;
using MyHttpServer.Framework;

namespace MyHttpServer;

[HttpController("api/homework")]
public sealed class TestController(TestMail mail)
{
    [HttpGet("send")]
    public Task<string> Send(HttpListenerContext context, [FromQuery] string to) =>
        mail.SendHomework(context.Request, to);
}
