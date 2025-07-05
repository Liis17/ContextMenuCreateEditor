using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ContextMenuCreateEditor.WPF.UserControls
{
    /// <summary>
    /// Логика взаимодействия для PreviewItem.xaml
    /// </summary>
    public partial class PreviewItem : UserControl
    {
        private string title { get; set; } = string.Empty;
        private string format { get; set; } = string.Empty;
        public PreviewItem(string _title, string _format)
        {
            InitializeComponent();
            Loaded += PreviewItem_Loaded;
            title = _title;
            format = _format;
        }

        private void PreviewItem_Loaded(object sender, RoutedEventArgs e)
        {
            IconItem.Source = FileIconHelper.GetFileIcon(format);
            TitleTextBlock.Text = title;
            FormatTextBlock.Text = format;
        }
    }
}
