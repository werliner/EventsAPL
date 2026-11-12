using EventsAPL.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EventsAPL;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Обработка наведения мыши на сайдбар
    private void Sidebar_MouseEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsSidebarHovered = true;
    }

    private void Sidebar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsSidebarHovered = false;
    }

    // Обработка боковых кнопок мыши (4 и 5)
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (DataContext is MainViewModel vm)
        {
            // Кнопка "Назад" (обычно 4-я)
            if (e.ChangedButton == MouseButton.XButton1)
            {
                if (vm.GoBackCommand.CanExecute(null))
                {
                    vm.GoBackCommand.Execute(null);
                    e.Handled = true;
                }
            }
            // Кнопка "Вперед" (обычно 5-я)
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                if (vm.GoForwardCommand.CanExecute(null))
                {
                    vm.GoForwardCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}