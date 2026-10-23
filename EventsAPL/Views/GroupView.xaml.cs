using System.Windows.Controls;
using System.Windows.Input;
using EventsAPL.ViewModels;

namespace EventsAPL.Views;

public partial class GroupsView : UserControl
{
    public GroupsView()
    {
        InitializeComponent();
    }

    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is GroupsViewModel vm)
        {
            vm.OpenEditCommand.Execute(null);
        }
    }
}