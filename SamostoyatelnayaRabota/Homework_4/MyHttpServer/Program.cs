using MyHttpServer.Framework.Server;
using MyHttpServer.Framework.Settings;
using MyHttpServer.Framework.Utils;

AppSettings settings = SettingsManager.Instance.Settings;
var server = new HttpServer(settings);

server.Start();
Log.Info("Расходы: http://127.0.0.1:8888/expenses");
Log.Info("Команды: stop — остановить сервер");

while (true)
{
    string? command = Console.ReadLine();
    if (command == null || command.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase))
        break;

    Log.Info($"Неизвестная команда: {command}");
}

server.Stop();
