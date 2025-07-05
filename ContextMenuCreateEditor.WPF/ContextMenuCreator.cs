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
    }
}
