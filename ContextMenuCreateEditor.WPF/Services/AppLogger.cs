using System;
using System.IO;

namespace ContextMenuCreateEditor.WPF.Services
{
    public static class AppLogger
    {
        private static readonly object Sync = new();
        public static string LogPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ContextMenuCreateEditor", "app.log");

        public static string AppDataRoot { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ContextMenuCreateEditor");

        static AppLogger()
        {
            try { Directory.CreateDirectory(AppDataRoot); } catch { }
        }

        public static void Info(string message) => Write("INFO", message, null);
        public static void Warn(string message) => Write("WARN", message, null);
        public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            try
            {
                lock (Sync)
                {
                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    if (ex != null) line += Environment.NewLine + ex;
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch { /* не падаем из-за лога */ }
        }
    }
}
