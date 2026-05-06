using System;
using System.IO;
using System.Linq;

namespace ContextMenuCreateEditor.WPF.Services
{
    /// <summary>
    /// CLI-режим. Запускается Explorer'ом через ShellNew\Command, когда нужно создать
    /// файл с точным именем (например, Dockerfile без расширения).
    /// </summary>
    public static class FileCreator
    {
        public static string CreateFile(string templatePath, string folderArg, string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                throw new ArgumentException("Имя файла пустое.");

            // Explorer может передать целевую папку или полный путь — нормализуем.
            string targetDir;
            if (Directory.Exists(folderArg))
            {
                targetDir = folderArg;
            }
            else
            {
                var dir = Path.GetDirectoryName(folderArg);
                targetDir = !string.IsNullOrEmpty(dir) && Directory.Exists(dir)
                    ? dir
                    : Environment.CurrentDirectory;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(baseName.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            if (string.IsNullOrEmpty(safe))
                throw new ArgumentException("Недопустимое имя файла.");

            var path = Path.Combine(targetDir, safe);
            if (File.Exists(path) || Directory.Exists(path))
            {
                var nameOnly = Path.GetFileNameWithoutExtension(safe);
                var ext = Path.GetExtension(safe);
                for (int n = 1; n < 1000; n++)
                {
                    var candidate = Path.Combine(targetDir, $"{nameOnly} ({n}){ext}");
                    if (!File.Exists(candidate) && !Directory.Exists(candidate))
                    {
                        path = candidate;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                File.Copy(templatePath, path, overwrite: false);
            else
                using (File.Create(path)) { }

            AppLogger.Info($"Создан файл {path} (template: {templatePath ?? "<пусто>"}).");
            return path;
        }
    }
}
