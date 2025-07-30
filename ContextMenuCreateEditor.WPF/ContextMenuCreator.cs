using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Text;

namespace ContextMenuCreateEditor.WPF
{
    public class ContextMenuCreator
    {
        public static void AddToNewMenu(string displayName, string fileExtension)
        {
            if (!fileExtension.StartsWith("."))
                fileExtension = "." + fileExtension;

            string className = "CustomShellNew" + Guid.NewGuid().ToString("N");

            // 1. Создаём ключ HKEY_CLASSES_ROOT\.ext
            using (RegistryKey extKey = Registry.ClassesRoot.CreateSubKey(fileExtension))
            {
                if (extKey == null)
                    throw new Exception("Не удалось создать ключ расширения файла");

                extKey.SetValue("", className);
            }

            // 2. Создаём ключ HKEY_CLASSES_ROOT\CustomShellNew...
            using (RegistryKey classKey = Registry.ClassesRoot.CreateSubKey(className))
            {
                if (classKey == null)
                    throw new Exception("Не удалось создать ключ класса файла");

                classKey.SetValue("", displayName);
            }

            // 3. Создаём ключ HKEY_CLASSES_ROOT\.ext\ShellNew
            using (RegistryKey shellNewKey = Registry.ClassesRoot.CreateSubKey(fileExtension + @"\ShellNew"))
            {
                if (shellNewKey == null)
                    throw new Exception("Не удалось создать ключ ShellNew");

                shellNewKey.SetValue("NullFile", ""); // создаёт пустой файл
            }

            Console.WriteLine($"Пункт 'Создать -> {displayName}' добавлен в контекстное меню.");
        }

        public static void RemoveFromNewMenu(string displayName, string fileExtension)
        {
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(fileExtension))
                throw new ArgumentException("Название или расширение файла не могут быть пустыми.");

            if (!fileExtension.StartsWith("."))
                fileExtension = "." + fileExtension;

            try
            {
                // 1. Проверяем ключ HKEY_CLASSES_ROOT\.ext
                using (RegistryKey extKey = Registry.ClassesRoot.OpenSubKey(fileExtension, writable: true))
                {
                    if (extKey == null)
                        throw new Exception($"Ключ для расширения {fileExtension} не найден.");

                    string className = extKey.GetValue("") as string;
                    if (string.IsNullOrEmpty(className))
                        throw new Exception($"Класс для {fileExtension} не определён.");

                    // 2. Удаляем HKEY_CLASSES_ROOT\.ext\ShellNew
                    try
                    {
                        Registry.ClassesRoot.DeleteSubKeyTree(fileExtension + @"\ShellNew");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Не удалось удалить ключ ShellNew для {fileExtension}: {ex.Message}");
                    }

                    // 3. Проверяем, используется ли className другими расширениями
                    bool isClassUsed = false;
                    foreach (var keyName in Registry.ClassesRoot.GetSubKeyNames())
                    {
                        if (keyName == fileExtension) continue;
                        using (RegistryKey otherKey = Registry.ClassesRoot.OpenSubKey(keyName))
                        {
                            if (otherKey?.GetValue("") as string == className)
                            {
                                isClassUsed = true;
                                break;
                            }
                        }
                    }

                    // 4. Если класс не используется, удаляем его
                    if (!isClassUsed)
                    {
                        try
                        {
                            Registry.ClassesRoot.DeleteSubKeyTree(className);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Не удалось удалить ключ класса {className}: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"Пункт 'Создать -> {displayName}' удалён из контекстного меню.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при удалении пункта меню: {ex.Message}", ex);
            }
        }
    }

}
