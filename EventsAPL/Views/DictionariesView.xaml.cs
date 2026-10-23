using System.Windows.Controls;
using System.Windows.Input;
using EventsAPL.ViewModels;

namespace EventsAPL.Views;

public partial class DictionariesView : UserControl
{
    public DictionariesView() => InitializeComponent();

    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DictionariesViewModel vm) vm.OpenEditCommand.Execute(null);
    }
}