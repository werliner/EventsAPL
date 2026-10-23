using EventsAPL.Base;
using System.Windows;

namespace EventsAPL.ViewModels;

public class MainViewModel : ViewModelBase
{
    // ---------- Наведена ли мышь на sidebar ----------
    private bool _isSidebarHovered;
    public bool IsSidebarHovered
    {
        get => _isSidebarHovered;
        set
        {
            if (Set(ref _isSidebarHovered, value))
                OnPropertyChanged(nameof(IsSidebarExpanded));
        }
    }

    // Sidebar раскрыт ТОЛЬКО когда на него наведена мышь
    public bool IsSidebarExpanded => IsSidebarHovered;

    // ---------- Текущий раздел ----------
    private string _currentTitle = "Мероприятия";
    public string CurrentTitle
    {
        get => _currentTitle;
        set => Set(ref _currentTitle, value);
    }

    private string _currentIcon = "\uE787";
    public string CurrentIcon
    {
        get => _currentIcon;
        set => Set(ref _currentIcon, value);
    }

    // ---------- Пользователь ----------
    private string _userDisplayName = "Гость";
    public string UserDisplayName
    {
        get => _userDisplayName;
        set => Set(ref _userDisplayName, value);
    }

    // ---------- Команды ----------
    public RelayCommand NavigateCommand { get; }
    public RelayCommand LoginCommand { get; }

    public MainViewModel()
    {
        NavigateCommand = new RelayCommand(param => Navigate(param as string));
        LoginCommand = new RelayCommand(_ => MessageBox.Show("Окно авторизации появится позже"));
    }

    private void Navigate(string? key)
    {
        (CurrentTitle, CurrentIcon) = key switch
        {
            "Events" => ("Мероприятия", "\uE787"),
            "Groups" => ("Группы", "\uE716"),
            "Students" => ("Студенты", "\uE77B"),
            "Teachers" => ("Преподаватели", "\uE7BE"),
            "Dictionaries" => ("Справочники", "\uE8F1"),
            "Log" => ("Журнал действий", "\uE81C"),
            "Admin" => ("Администрирование", "\uE713"),
            _ => (CurrentTitle, CurrentIcon)
        };
    }
}