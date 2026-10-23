using System.Windows.Controls;
using System.Windows.Input;
using EventsAPL.ViewModels;

namespace EventsAPL.Views;

public partial class TeachersView : UserControl
{
    public TeachersView() => InitializeComponent();

    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is TeachersViewModel vm) vm.OpenEditCommand.Execute(null);
    }
}