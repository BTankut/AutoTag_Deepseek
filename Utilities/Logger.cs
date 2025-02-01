using System;

namespace TagsOrderingPlugin.Utilities
{
    public static class Logger
    {
        public static void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {message}");
        }

        public static void LogDebug(string message)
        {
            Console.WriteLine($"[DEBUG] {message}");
        }

        public static void LogError(string message, Exception ex = null)
        {
            Console.WriteLine($"[ERROR] {message}");
            if (ex != null)
            {
                Console.WriteLine($"[ERROR] Exception: {ex.Message}");
                Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
            }
        }
    }
}
