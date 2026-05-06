using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ContextMenuCreateEditor.WPF.Services
{
    public class IconService : IIconService
    {
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
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHSTOCKICONINFO
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysImageIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint SHGFI_SMALLICON = 0x000000000;
        private const uint SHGSI_ICON = 0x000000100;
        private const int SIID_DOCNOASSOC = 0;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetStockIconInfo(int siid, uint uFlags, ref SHSTOCKICONINFO psii);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public BitmapSource? GetFileIcon(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return GetDefaultIcon();
            if (!extension.StartsWith(".")) extension = "." + extension;

            var shinfo = new SHFILEINFO();
            SHGetFileInfo(extension, 0x80, ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | SHGFI_SMALLICON);

            if (shinfo.hIcon == IntPtr.Zero)
                return GetDefaultIcon();

            try
            {
                return Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"CreateBitmapSourceFromHIcon упал для '{extension}': {ex.Message}");
                return GetDefaultIcon();
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }

        private static BitmapSource? GetDefaultIcon()
        {
            var sii = new SHSTOCKICONINFO { cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO)) };
            int hr = SHGetStockIconInfo(SIID_DOCNOASSOC, SHGSI_ICON, ref sii);
            if (hr != 0 || sii.hIcon == IntPtr.Zero) return null;

            try
            {
                return Imaging.CreateBitmapSourceFromHIcon(sii.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch
            {
                return null;
            }
            finally
            {
                DestroyIcon(sii.hIcon);
            }
        }
    }
}
