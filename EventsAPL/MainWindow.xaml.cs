using EventsAPL.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace EventsAPL;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

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
}