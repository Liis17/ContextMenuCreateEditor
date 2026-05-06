using ContextMenuCreateEditor.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ContextMenuCreateEditor.WPF.UserControls
{
    public partial class PreviewItem : UserControl
    {
        public static readonly RoutedEvent EditRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(EditRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PreviewItem));
        public static readonly RoutedEvent DeleteRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(DeleteRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PreviewItem));

        public event RoutedEventHandler EditRequested
        {
            add => AddHandler(EditRequestedEvent, value);
            remove => RemoveHandler(EditRequestedEvent, value);
        }

        public event RoutedEventHandler DeleteRequested
        {
            add => AddHandler(DeleteRequestedEvent, value);
            remove => RemoveHandler(DeleteRequestedEvent, value);
        }

        public PreviewItem()
        {
            InitializeComponent();
        }

        private bool IsOwn => DataContext is ShellNewItemViewModel vm && vm.IsOwn;

        private void EditBtn_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(EditRequestedEvent, this));

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(DeleteRequestedEvent, this));

        private void MainBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!IsOwn) return;
            ActionsPanel.Visibility = Visibility.Visible;
            ActionsPanel.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = ActionsPanel.Opacity,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.15)
            });
        }

        private void MainBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var anim = new DoubleAnimation
            {
                From = ActionsPanel.Opacity,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.15)
            };
            anim.Completed += (_, _) => { if (ActionsPanel.Opacity == 0) ActionsPanel.Visibility = Visibility.Collapsed; };
            ActionsPanel.BeginAnimation(OpacityProperty, anim);
        }
    }
}
