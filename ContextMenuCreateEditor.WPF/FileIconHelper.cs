using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ContextMenuCreateEditor.WPF
{
    public class FileIconHelper
    {// Структура SHFILEINFO
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        // Флаги для SHGetFileInfo
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_SMALLICON = 0x000000000;

        // Внешняя функция Windows API
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        /// <summary>
        /// Возвращает системную иконку, ассоциированную с расширением файла
        /// </summary>
        public static BitmapSource GetFileIcon(string extension)
        {
            if (!extension.StartsWith("."))
                extension = "." + extension;

            var shinfo = new SHFILEINFO();
            IntPtr hImg = SHGetFileInfo(
                extension,
                0x80, // FILE_ATTRIBUTE_NORMAL
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | SHGFI_SMALLICON);

            if (shinfo.hIcon == IntPtr.Zero)
                return null;

            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Освобождаем ресурсы
            DestroyIcon(shinfo.hIcon);
            return source;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
