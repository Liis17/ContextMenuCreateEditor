using ContextMenuCreateEditor.WPF.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ContextMenuCreateEditor.WPF.Services
{
    /// <summary>
    /// Все операции — в HKEY_CURRENT_USER\Software\Classes. UAC не требуется.
    /// Поддерживает два режима ShellNew:
    ///  • Стандартный (FileName/NullFile) — Explorer создаёт файл «Новый &lt;FriendlyName&gt;.&lt;ext&gt;».
    ///  • Command — Explorer запускает наше приложение, оно создаёт файл с точным именем
    ///    (для случаев типа Dockerfile, Makefile, .gitignore — без или с произвольным расширением).
    /// </summary>
    public class HkcuRegistryService : IRegistryService
    {
        public const string CreatedByValue = "ContextMenuCreateEditor";
        private const string ClassesPath = @"Software\Classes";

        private static readonly Regex ExtRegex = new(@"^[a-zA-Z0-9]{1,16}$", RegexOptions.Compiled);
        private static readonly Regex CommandRegex = new(
            @"--create\s+""([^""]*)""\s+""[^""]*""\s+""([^""]+)""",
            RegexOptions.Compiled);
        private static readonly char[] InvalidNameChars = "<>:\"/\\|?*".ToCharArray();

        public IReadOnlyList<ShellNewItem> GetItems()
        {
            var result = new List<ShellNewItem>();

            using var classes = Registry.CurrentUser.OpenSubKey(ClassesPath);
            if (classes == null) return result;

            foreach (var subKeyName in classes.GetSubKeyNames())
            {
                if (!subKeyName.StartsWith(".")) continue;

                try
                {
                    var item = ReadItem(classes, subKeyName);
                    if (item != null) result.Add(item);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Не удалось прочитать ключ '{subKeyName}': {ex.Message}");
                }
            }

            return result;
        }

        public AddResult TryAdd(string displayName, string fileName, string extension, string templatePath)
        {
            if (!ValidateExtension(extension, out var normalizedExt, out var extError))
                throw new ArgumentException(extError);
            if (!ValidateName(displayName, out var dnErr)) throw new ArgumentException(dnErr);
            if (!ValidateName(fileName, out var fnErr)) throw new ArgumentException(fnErr);

            bool useCommandMode = string.IsNullOrEmpty(normalizedExt);
            normalizedExt = ResolveExtensionKey(normalizedExt, fileName);

            using var classes = Registry.CurrentUser.CreateSubKey(ClassesPath, writable: true)
                ?? throw new InvalidOperationException("Не удалось открыть HKCU\\Software\\Classes.");

            using var existingExt = classes.OpenSubKey(normalizedExt, writable: false);
            string? existingProgId = existingExt?.GetValue("") as string;

            if (!string.IsNullOrEmpty(existingProgId))
            {
                bool ours = IsOwnProgId(classes, normalizedExt, existingProgId);
                if (!ours)
                {
                    return new AddResult
                    {
                        Outcome = AddOutcome.Conflict,
                        ExistingProgId = existingProgId
                    };
                }
                WriteProgIdAndShellNew(classes, normalizedExt, existingProgId, displayName, fileName, templatePath, createNewProgId: false, useCommandMode);
                return new AddResult
                {
                    Outcome = AddOutcome.Updated,
                    ExistingProgId = existingProgId,
                    Item = ReadItem(classes, normalizedExt)
                };
            }

            var newProgId = "CustomShellNew" + Guid.NewGuid().ToString("N");
            WriteProgIdAndShellNew(classes, normalizedExt, newProgId, displayName, fileName, templatePath, createNewProgId: true, useCommandMode);
            return new AddResult
            {
                Outcome = AddOutcome.Created,
                ExistingProgId = newProgId,
                Item = ReadItem(classes, normalizedExt)
            };
        }

        public AddResult ForceAdd(string displayName, string fileName, string extension, string templatePath)
        {
            if (!ValidateExtension(extension, out var normalizedExt, out var extError))
                throw new ArgumentException(extError);
            if (!ValidateName(displayName, out var dnErr)) throw new ArgumentException(dnErr);
            if (!ValidateName(fileName, out var fnErr)) throw new ArgumentException(fnErr);

            bool useCommandMode = string.IsNullOrEmpty(normalizedExt);
            normalizedExt = ResolveExtensionKey(normalizedExt, fileName);

            using var classes = Registry.CurrentUser.CreateSubKey(ClassesPath, writable: true)
                ?? throw new InvalidOperationException("Не удалось открыть HKCU\\Software\\Classes.");

            var newProgId = "CustomShellNew" + Guid.NewGuid().ToString("N");
            WriteProgIdAndShellNew(classes, normalizedExt, newProgId, displayName, fileName, templatePath, createNewProgId: true, useCommandMode);
            AppLogger.Warn($"Принудительная перезапись ProgID для {normalizedExt} → {newProgId}.");

            return new AddResult
            {
                Outcome = AddOutcome.Created,
                ExistingProgId = newProgId,
                Item = ReadItem(classes, normalizedExt)
            };
        }

        public AddResult AddUsingExistingProgId(string displayName, string fileName, string extension, string templatePath, string existingProgId)
        {
            if (!ValidateExtension(extension, out var normalizedExt, out var extError))
                throw new ArgumentException(extError);

            bool useCommandMode = string.IsNullOrEmpty(normalizedExt);
            normalizedExt = ResolveExtensionKey(normalizedExt, fileName);

            using var classes = Registry.CurrentUser.CreateSubKey(ClassesPath, writable: true)
                ?? throw new InvalidOperationException("Не удалось открыть HKCU\\Software\\Classes.");

            using (var extKey = classes.CreateSubKey(normalizedExt, writable: true))
            {
                if (extKey?.GetValue("") is null)
                    extKey?.SetValue("", existingProgId);
            }

            using (var shellNew = classes.CreateSubKey($@"{normalizedExt}\ShellNew", writable: true)
                ?? throw new InvalidOperationException("Не удалось создать ShellNew."))
            {
                ClearShellNewData(shellNew);
                WriteShellNewData(shellNew, fileName, templatePath, useCommandMode);
                shellNew.SetValue("CreatedBy", CreatedByValue);
            }

            return new AddResult
            {
                Outcome = AddOutcome.Created,
                ExistingProgId = existingProgId,
                Item = ReadItem(classes, normalizedExt)
            };
        }

        public void Update(ShellNewItem original, ShellNewItem updated)
        {
            if (!original.IsOwn)
                throw new InvalidOperationException("Изменение системных пунктов запрещено.");

            // Если расширение фактически изменилось — удаляем старое и создаём новое.
            // Особый случай: исходное было derived (.<filename>) и пользователь снова не указал ext —
            // расширение тоже могло поменяться, если изменилось имя файла.
            var origExt = original.Extension;
            var newRawExt = updated.Extension;
            bool needRecreate;
            if (string.IsNullOrEmpty(newRawExt))
            {
                var derived = ResolveExtensionKey(string.Empty, updated.FileName);
                needRecreate = !string.Equals(origExt, derived, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                needRecreate = !string.Equals(origExt, newRawExt, StringComparison.OrdinalIgnoreCase);
            }

            if (needRecreate)
            {
                Remove(original);
                TryAdd(updated.DisplayName, updated.FileName, updated.Extension, updated.TemplatePath ?? string.Empty);
                return;
            }

            using var classes = Registry.CurrentUser.CreateSubKey(ClassesPath, writable: true)
                ?? throw new InvalidOperationException("Не удалось открыть HKCU\\Software\\Classes.");

            var progId = original.ProgId ?? throw new InvalidOperationException("ProgID отсутствует.");
            // Сохраняем тот же режим, в котором был создан: если оригинал использовал Command — продолжаем.
            bool useCommandMode = original.UseCommandMode || string.IsNullOrEmpty(updated.Extension);
            WriteProgIdAndShellNew(classes, original.Extension, progId, updated.DisplayName, updated.FileName, updated.TemplatePath ?? string.Empty, createNewProgId: false, useCommandMode);
        }

        public void Remove(ShellNewItem item)
        {
            if (!item.IsOwn)
                throw new InvalidOperationException("Удаление системных пунктов запрещено.");

            using var classes = Registry.CurrentUser.OpenSubKey(ClassesPath, writable: true);
            if (classes == null) return;

            try
            {
                classes.DeleteSubKeyTree($@"{item.Extension}\ShellNew", throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Не удалось удалить ShellNew для {item.Extension}: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(item.ProgId))
            {
                bool ours = false;
                using (var classKey = classes.OpenSubKey(item.ProgId))
                {
                    ours = (classKey?.GetValue("CreatedBy") as string) == CreatedByValue;
                }

                if (ours)
                {
                    try { classes.DeleteSubKeyTree(item.ProgId, throwOnMissingSubKey: false); }
                    catch (Exception ex) { AppLogger.Warn($"Не удалось удалить ProgID {item.ProgId}: {ex.Message}"); }

                    try { classes.DeleteSubKeyTree(item.Extension, throwOnMissingSubKey: false); }
                    catch (Exception ex) { AppLogger.Warn($"Не удалось удалить расширение {item.Extension}: {ex.Message}"); }
                }
            }
        }

        public bool ValidateExtension(string extension, out string normalized, out string? error)
        {
            normalized = string.Empty;
            error = null;
            if (string.IsNullOrWhiteSpace(extension))
                return true;
            var trimmed = extension.Trim().TrimStart('.');
            if (!ExtRegex.IsMatch(trimmed))
            {
                error = "Расширение должно содержать 1-16 латинских букв или цифр (или быть пустым).";
                return false;
            }
            normalized = "." + trimmed.ToLowerInvariant();
            return true;
        }

        public bool ValidateName(string name, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Поле не может быть пустым.";
                return false;
            }
            if (name.IndexOfAny(InvalidNameChars) >= 0)
            {
                error = "Имя содержит недопустимые символы (< > : \" / \\ | ? *).";
                return false;
            }
            return true;
        }

        // ---------- private ----------

        private static string ResolveExtensionKey(string normalizedExt, string fileName)
        {
            if (!string.IsNullOrEmpty(normalizedExt)) return normalizedExt;
            var sanitized = new string((fileName ?? string.Empty)
                .Where(ch => char.IsLetterOrDigit(ch))
                .ToArray());
            if (string.IsNullOrEmpty(sanitized))
                throw new ArgumentException("Не удалось вывести расширение: укажите расширение или имя файла из латинских букв/цифр.");
            return "." + sanitized.ToLowerInvariant();
        }

        private static bool IsOwnProgId(RegistryKey classes, string extension, string progId)
        {
            try
            {
                using (var classKey = classes.OpenSubKey(progId))
                {
                    if ((classKey?.GetValue("CreatedBy") as string) == CreatedByValue)
                        return true;
                }
                using (var shellNew = classes.OpenSubKey($@"{extension}\ShellNew"))
                {
                    if ((shellNew?.GetValue("CreatedBy") as string) == CreatedByValue)
                        return true;
                }
                if (progId.StartsWith("CustomShellNew", StringComparison.Ordinal))
                    return true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"IsOwnProgId упал: {ex.Message}");
            }
            return false;
        }

        private static void WriteProgIdAndShellNew(RegistryKey classes, string extension, string progId,
            string displayName, string fileName, string templatePath, bool createNewProgId, bool useCommandMode)
        {
            using (var extKey = classes.CreateSubKey(extension, writable: true)
                ?? throw new InvalidOperationException("Не удалось создать ключ расширения."))
            {
                extKey.SetValue("", progId);
            }

            using (var classKey = classes.CreateSubKey(progId, writable: true)
                ?? throw new InvalidOperationException("Не удалось создать ключ ProgID."))
            {
                classKey.SetValue("", displayName);
                if (createNewProgId || (classKey.GetValue("CreatedBy") as string) == CreatedByValue)
                {
                    classKey.SetValue("CreatedBy", CreatedByValue);
                }
            }

            using (var shellNew = classes.CreateSubKey($@"{extension}\ShellNew", writable: true)
                ?? throw new InvalidOperationException("Не удалось создать ShellNew."))
            {
                ClearShellNewData(shellNew);
                WriteShellNewData(shellNew, fileName, templatePath, useCommandMode);
                shellNew.SetValue("CreatedBy", CreatedByValue);
            }
        }

        private static void WriteShellNewData(RegistryKey shellNew, string fileName, string templatePath, bool useCommandMode)
        {
            if (useCommandMode)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу приложения.");
                var template = templatePath ?? string.Empty;
                // Формат: "exe" --create "templatePath" "%1" "baseName"
                // %1 — Explorer подставит путь к целевой папке.
                var command = $"\"{exePath}\" --create \"{template}\" \"%1\" \"{fileName}\"";
                shellNew.SetValue("Command", command);
            }
            else if (string.IsNullOrEmpty(templatePath))
            {
                shellNew.SetValue("NullFile", "");
            }
            else
            {
                shellNew.SetValue("FileName", templatePath);
            }
        }

        private static void ClearShellNewData(RegistryKey shellNew)
        {
            foreach (var name in new[] { "NullFile", "FileName", "Data", "Command" })
            {
                try { if (shellNew.GetValue(name) != null) shellNew.DeleteValue(name, throwOnMissingValue: false); }
                catch { }
            }
        }

        private ShellNewItem? ReadItem(RegistryKey classes, string extension)
        {
            using var shellNew = classes.OpenSubKey($@"{extension}\ShellNew");
            if (shellNew == null) return null;

            using var extKey = classes.OpenSubKey(extension);
            var progId = extKey?.GetValue("") as string;
            if (string.IsNullOrEmpty(progId)) return null;

            string displayName = progId;
            using (var classKey = classes.OpenSubKey(progId))
            {
                if (classKey?.GetValue("") is string fn && !string.IsNullOrEmpty(fn))
                    displayName = fn;
            }

            var commandValue = shellNew.GetValue("Command") as string;
            var fileNameValue = shellNew.GetValue("FileName") as string;

            string template = string.Empty;
            string fileBase = displayName;
            string? templatePath = null;
            bool useCommandMode = false;

            if (!string.IsNullOrEmpty(commandValue))
            {
                useCommandMode = true;
                var match = CommandRegex.Match(commandValue);
                if (match.Success)
                {
                    templatePath = match.Groups[1].Value;
                    fileBase = match.Groups[2].Value;
                    if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                    {
                        try { template = File.ReadAllText(templatePath); } catch { }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(fileNameValue))
            {
                templatePath = fileNameValue;
                if (File.Exists(fileNameValue))
                {
                    try { template = File.ReadAllText(fileNameValue); } catch { }
                    fileBase = Path.GetFileNameWithoutExtension(fileNameValue);
                }
            }

            return new ShellNewItem
            {
                DisplayName = displayName,
                FileName = fileBase,
                Extension = extension,
                Template = template,
                TemplatePath = templatePath,
                ProgId = progId,
                IsOwn = (shellNew.GetValue("CreatedBy") as string) == CreatedByValue,
                UseCommandMode = useCommandMode
            };
        }
    }
}
