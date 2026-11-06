using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EventsAPL.ViewModels;

namespace EventsAPL.Views;

public partial class EventsView : UserControl
{
    public EventsView()
    {
        InitializeComponent();

        this.Loaded += EventsView_Loaded;
        this.Unloaded += EventsView_Unloaded;
    }

    private void EventsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EventsViewModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void EventsView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EventsViewModel vm)
        {
            vm.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EventsViewModel.IsEditing) && sender is EventsViewModel vm && !vm.IsEditing)
        {
            CloseAllPopups(this);
        }
    }

    private void CloseAllPopups(DependencyObject? parent)
    {
        if (parent == null) return;

        if (parent is DatePicker dp) dp.IsDropDownOpen = false;
        if (parent is ComboBox cb) cb.IsDropDownOpen = false;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            CloseAllPopups(VisualTreeHelper.GetChild(parent, i));
        }
    }

    // ВОТ ТОТ САМЫЙ МЕТОД ДЛЯ ДВОЙНОГО КЛИКА
    private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is EventsViewModel vm)
            vm.OpenEditCommand.Execute(null);
    }
}