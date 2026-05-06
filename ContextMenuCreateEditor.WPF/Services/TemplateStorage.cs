using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ContextMenuCreateEditor.WPF.Services
{
    public class TemplateStorage : ITemplateStorage
    {
        public string TemplatesRoot { get; }

        public TemplateStorage()
        {
            TemplatesRoot = Path.Combine(AppLogger.AppDataRoot, "Templates");
            Directory.CreateDirectory(TemplatesRoot);
        }

        public string SaveTemplate(string fileName, string extension, string content)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Имя файла не может быть пустым.", nameof(fileName));

            // Расширение опционально (Dockerfile, Makefile и т.п.).
            if (!string.IsNullOrEmpty(extension) && !extension.StartsWith("."))
                extension = "." + extension;
            extension ??= string.Empty;

            var safeName = Sanitize(fileName);
            var path = Path.Combine(TemplatesRoot, safeName + extension);

            // Если уже занят другим item — добавим короткий хеш контента, чтобы не затереть
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path);
                if (existing != content)
                {
                    var hash = ShortHash(safeName + extension + content);
                    path = Path.Combine(TemplatesRoot, $"{safeName}_{hash}{extension}");
                }
            }

            File.WriteAllText(path, content ?? string.Empty, new UTF8Encoding(false));
            return path;
        }

        public bool DeleteTemplate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Не удалось удалить шаблон '{path}': {ex.Message}");
            }
            return false;
        }

        public string? ReadTemplate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Не удалось прочитать шаблон '{path}': {ex.Message}");
                return null;
            }
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(invalid.Contains(ch) ? '_' : ch);
            var s = sb.ToString().Trim();
            return string.IsNullOrEmpty(s) ? "template" : s;
        }

        private static string ShortHash(string s)
        {
            using var sha = SHA1.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
        }
    }
}
