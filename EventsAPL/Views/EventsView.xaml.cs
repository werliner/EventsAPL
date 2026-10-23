using System.Windows.Controls;
using System.Windows.Input;
using EventsAPL.ViewModels;

namespace EventsAPL.Views;

public partial class EventsView : UserControl
{
    public EventsView()
    {
        InitializeComponent();
    }

    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is EventsViewModel vm)
            vm.OpenEditCommand.Execute(null);
    }
}