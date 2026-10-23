using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EventsAPL.Base;

/// <summary>
/// Базовый класс для всех ViewModel.
/// Реализует INotifyPropertyChanged — механизм, который сообщает UI,
/// что данные изменились, и UI перерисовывается.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Обновляет приватное поле и уведомляет UI.
    /// Возвращает true, если значение действительно изменилось.
    /// </summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}