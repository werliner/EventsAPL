using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;

namespace EventsAPL.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly GroupRepository _groupRepository;
    private readonly DictionaryRepository _dictionaryRepository = null!;
    private readonly EventRepository _eventRepository = null!;
    private readonly StudentRepository _studentRepository;
    private readonly TeacherRepository _teacherRepository; // <-- НОВОЕ

    private readonly Stack<ViewModelBase> _backStack = new();
    private readonly Stack<ViewModelBase> _forwardStack = new();

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

    public bool IsSidebarExpanded => IsSidebarHovered;

    private ViewModelBase? _currentViewModel;
    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        set => Set(ref _currentViewModel, value);
    }

    private string _userDisplayName = "Гость";
    public string UserDisplayName
    {
        get => _userDisplayName;
        set => Set(ref _userDisplayName, value);
    }

    public RelayCommand NavigateCommand { get; }
    public RelayCommand LoginCommand { get; }
    public RelayCommand GoBackCommand { get; }
    public RelayCommand GoForwardCommand { get; }

    public MainViewModel()
    {
        try
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            var config = JsonDocument.Parse(File.ReadAllText(configPath)).RootElement.GetProperty("Turso");

            string url = config.GetProperty("Url").GetString()!;
            string token = config.GetProperty("Token").GetString()!;

            var client = new TursoClient(url, token);
            _groupRepository = new GroupRepository(client);
            _studentRepository = new StudentRepository(client);
            _teacherRepository = new TeacherRepository(client);
            _dictionaryRepository = new DictionaryRepository(client);
            _eventRepository = new EventRepository(client);

            NavigateCommand = new RelayCommand(param => Navigate(param as string));
            LoginCommand = new RelayCommand(_ => MessageBox.Show("Окно авторизации появится позже"));

            GoBackCommand = new RelayCommand(_ => GoBack(), _ => _backStack.Count > 0);
            GoForwardCommand = new RelayCommand(_ => GoForward(), _ => _forwardStack.Count > 0);

            Navigate("Events");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при инициализации:\n{ex.Message}", "Критическая ошибка");
        }
    }

    private void Navigate(string? key)
    {
        ViewModelBase newViewModel = key switch
        {
            "Groups" => new GroupsViewModel(_groupRepository, NavigateToStudents),
            "Students" => new StudentsViewModel(_studentRepository, _groupRepository),
            "Teachers" => new TeachersViewModel(_teacherRepository),
            "Dictionaries" => new DictionariesViewModel(_dictionaryRepository),
            "Events" => new EventsViewModel(_eventRepository, _dictionaryRepository),
            _ => new PlaceholderViewModel("Раздел в разработке")
        };

        NavigateTo(newViewModel);
    }

    private void NavigateToStudents(Group group)
    {
        NavigateTo(new StudentsViewModel(_studentRepository, _groupRepository, group.Id));
    }

    private void NavigateTo(ViewModelBase newViewModel)
    {
        if (CurrentViewModel != null)
        {
            _backStack.Push(CurrentViewModel);
            _forwardStack.Clear();
        }
        CurrentViewModel = newViewModel;
        UpdateNavigationButtons();
    }

    private void GoBack()
    {
        if (_backStack.Count > 0)
        {
            if (CurrentViewModel != null) _forwardStack.Push(CurrentViewModel);
            CurrentViewModel = _backStack.Pop();
            UpdateNavigationButtons();
        }
    }

    private void GoForward()
    {
        if (_forwardStack.Count > 0)
        {
            if (CurrentViewModel != null) _backStack.Push(CurrentViewModel);
            CurrentViewModel = _forwardStack.Pop();
            UpdateNavigationButtons();
        }
    }

    private void UpdateNavigationButtons()
    {
        GoBackCommand.RaiseCanExecuteChanged();
        GoForwardCommand.RaiseCanExecuteChanged();
    }
}