using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Text;

namespace ContextMenuCreateEditor.WPF
{
    public class ContextMenuReader
    {
        public static List<(string DisplayName, string Extension)> GetRegisteredNewItems()
        {
            var result = new List<(string, string)>();

            using (var classesRoot = Registry.ClassesRoot)
            {
                foreach (var subKeyName in classesRoot.GetSubKeyNames())
                {
                    // Проверяем, что это расширение (начинается с точки)
                    if (!subKeyName.StartsWith("."))
                        continue;

                    try
                    {
                        // Проверяем, есть ли подпапка ShellNew
                        using (var shellNewKey = classesRoot.OpenSubKey($@"{subKeyName}\ShellNew"))
                        {
                            if (shellNewKey == null)
                                continue;

                            // Получаем имя класса из расширения
                            using (var extKey = classesRoot.OpenSubKey(subKeyName))
                            {
                                string className = extKey?.GetValue("") as string;
                                if (string.IsNullOrEmpty(className))
                                    continue;

                                // Получаем отображаемое имя из ключа класса
                                using (var classKey = classesRoot.OpenSubKey(className))
                                {
                                    string displayName = classKey?.GetValue("") as string ?? className;
                                    result.Add((displayName, subKeyName));
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки чтения (например, доступа)
                        continue;
                    }
                }
            }

            return result;
        }
    }
}
