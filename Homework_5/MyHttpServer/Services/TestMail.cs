using System.Net;
using System.Net.Mail;

namespace MyHttpServer;

public sealed class TestMail
{
    public async Task<string> SendHomework(HttpListenerRequest request, string to)
    {
        if (request.Headers["Sec-Fetch-Site"] == "cross-site")
            return "[EMAIL ERROR]: Запрос с другого сайта.";
        if (!MailAddress.TryCreate(to, out var recipient) ||
            !string.Equals(recipient.Address, to, StringComparison.OrdinalIgnoreCase))
            return "[EMAIL ERROR]: Некорректный адрес.";

        string? host = Environment.GetEnvironmentVariable("ORIS_SMTP_HOST");
        string? allowed = Environment.GetEnvironmentVariable("ORIS_SMTP_TO");
        if (!string.IsNullOrWhiteSpace(host) && !string.Equals(allowed, recipient.Address, StringComparison.OrdinalIgnoreCase))
            return "[EMAIL ERROR]: Получатель не настроен.";

        string from = Environment.GetEnvironmentVariable("ORIS_SMTP_FROM") ?? "server@example.test";
        string text = Environment.GetEnvironmentVariable("ORIS_HOMEWORK_URL") ?? "Тестовое письмо для домашнего задания.";
        using var message = new MailMessage(new MailAddress(from), recipient) { Subject = "Homework", Body = text };
        using var client = new SmtpClient();
        if (string.IsNullOrWhiteSpace(host))
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "mail");
            Directory.CreateDirectory(folder);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = folder;
        }
        else
        {
            client.Host = host;
            client.Port = int.TryParse(Environment.GetEnvironmentVariable("ORIS_SMTP_PORT"), out var port) ? port : 587;
            client.EnableSsl = true;
            string? user = Environment.GetEnvironmentVariable("ORIS_SMTP_USER");
            if (!string.IsNullOrWhiteSpace(user))
                client.Credentials = new NetworkCredential(user, Environment.GetEnvironmentVariable("ORIS_SMTP_PASSWORD"));
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await client.SendMailAsync(message, timeout.Token);
            return "[EMAIL] Успешно отправлено";
        }
        catch (Exception error) when (error is SmtpException or OperationCanceledException)
        {
            return "[EMAIL ERROR]: Не удалось отправить письмо.";
        }
    }
}
