using EventsAPL.Base;
namespace EventsAPL.ViewModels;

public class PlaceholderViewModel : ViewModelBase
{
    public string Text { get; }
    public PlaceholderViewModel(string text) => Text = text;
}