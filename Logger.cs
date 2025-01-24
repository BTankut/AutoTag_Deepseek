using System;
using System.Diagnostics;
using System.IO;

namespace TagsOrderingPlugin
{
    /// <summary>
    /// Uygulama loglarını yöneten sınıf
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TagsOrdering.log");

        private static readonly object LockObj = new object();

        /// <summary>
        /// Bilgi mesajı loglar
        /// </summary>
        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        /// <summary>
        /// Hata mesajı loglar
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            Log("ERROR", message);
            if (ex != null)
            {
                Log("ERROR", $"Exception: {ex.Message}");
                Log("ERROR", $"StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Debug mesajı loglar
        /// </summary>
        public static void LogDebug(string message)
        {
            #if DEBUG
            Log("DEBUG", message);
            #endif
        }

        /// <summary>
        /// Uyarı mesajı loglar
        /// </summary>
        public static void LogWarning(string message)
        {
            Log("WARNING", message);
        }

        /// <summary>
        /// Başarı mesajı loglar
        /// </summary>
        public static void LogSuccess(string message)
        {
            Log("SUCCESS", message);
        }

        private static void Log(string level, string message)
        {
            try
            {
                lock (LockObj)
                {
                    string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                    File.AppendAllText(LogPath, logMessage + Environment.NewLine);
                    Debug.WriteLine(logMessage);
                }
            }
            catch
            {
                // Loglama hatalarını sessizce geç
            }
        }

        /// <summary>
        /// Log dosyasını temizler
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }
            }
            catch
            {
                // Temizleme hatalarını sessizce geç
            }
        }
    }
}
