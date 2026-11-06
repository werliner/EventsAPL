using System.Windows;
using EventsAPL.ViewModels;

namespace EventsAPL.Views;

public partial class EventParticipantsView : Window
{
    public EventParticipantsView(EventParticipantsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.CloseAction = () => this.Close();
    }
}