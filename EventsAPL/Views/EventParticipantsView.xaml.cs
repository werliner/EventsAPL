using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    // Обработчик клика по карточке
    private void ParticipantCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Игнорируем клики по ComboBox и CheckBox
        var originalSource = e.OriginalSource as DependencyObject;
        if (FindParent<ComboBox>(originalSource) != null) return;
        if (FindParent<CheckBox>(originalSource) != null) return;

        if (sender is FrameworkElement fe)
        {
            if (fe.DataContext is SelectableStudent student)
                student.IsSelected = !student.IsSelected;
            else if (fe.DataContext is SelectableTeacher teacher)
                teacher.IsSelected = !teacher.IsSelected;
        }
    }

    // Вспомогательный метод для поиска родителя в визуальном дереве
    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;
        var parent = VisualTreeHelper.GetParent(child);
        if (parent == null) return null;
        if (parent is T typedParent) return typedParent;
        return FindParent<T>(parent);
    }
}