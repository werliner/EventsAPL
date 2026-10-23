using System.Windows.Controls;
using System.Windows.Input;
using EventsAPL.ViewModels;

namespace EventsAPL.Views;

public partial class StudentsView : UserControl
{
    public StudentsView()
    {
        InitializeComponent();
    }

    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is StudentsViewModel vm)
            vm.OpenEditCommand.Execute(null);
    }
}