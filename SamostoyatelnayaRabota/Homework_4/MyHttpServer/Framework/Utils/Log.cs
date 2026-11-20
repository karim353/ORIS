namespace MyHttpServer.Framework.Utils;

public static class Log
{
    private static readonly object Sync = new();

    public static void Info(string message) => Write("INFO ", message, ConsoleColor.Gray);
    public static void Error(string message) => Write("ERROR", message, ConsoleColor.Red);

    private static void Write(string level, string message, ConsoleColor color)
    {
        lock (Sync)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level} {message}");
            Console.ForegroundColor = previous;
        }
    }
}
