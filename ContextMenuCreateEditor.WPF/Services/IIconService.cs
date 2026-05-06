using System.Windows.Media.Imaging;

namespace ContextMenuCreateEditor.WPF.Services
{
    public interface IIconService
    {
        BitmapSource? GetFileIcon(string extension);
    }
}
