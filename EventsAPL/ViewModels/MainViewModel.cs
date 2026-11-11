using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace EventsAPL.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly GroupRepository _groupRepository = null!;
    private readonly StudentRepository _studentRepository = null!;
    private readonly TeacherRepository _teacherRepository = null!;
    private readonly DictionaryRepository _dictionaryRepository = null!;
    private readonly EventRepository _eventRepository = null!;
    private readonly ParticipantRepository _participantRepository = null!;

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

    public RelayCommand NavigateCommand { get; } = null!;
    public RelayCommand LoginCommand { get; } = null!;
    public RelayCommand GoBackCommand { get; } = null!;
    public RelayCommand GoForwardCommand { get; } = null!;
    public RelayCommand ImportStudentsFromWordCommand { get; } = null!;

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
            _participantRepository = new ParticipantRepository(client);

            NavigateCommand = new RelayCommand(param => Navigate(param as string));
            LoginCommand = new RelayCommand(_ => MessageBox.Show("Окно авторизации появится позже"));

            GoBackCommand = new RelayCommand(_ => GoBack(), _ => _backStack.Count > 0);
            GoForwardCommand = new RelayCommand(_ => GoForward(), _ => _forwardStack.Count > 0);

            // ▼▼▼ Импорт из Word — ВНУТРИ try, где client доступен ▼▼▼
            ImportStudentsFromWordCommand = new RelayCommand(async _ =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Word Documents (*.docx)|*.docx",
                    Multiselect = true,
                    Title = "Выберите файлы со списками групп"
                };

                if (dlg.ShowDialog() != true) return;

                try
                {
                    var importer = new StudentListImporter(client, _groupRepository, _studentRepository);
                    var fullReport = new StringBuilder();
                    fullReport.AppendLine("=== РЕЗУЛЬТАТЫ ИМПОРТА ===\n");

                    foreach (var file in dlg.FileNames)
                    {
                        var result = await importer.ImportFromDocxAsync(file);
                        fullReport.AppendLine(result);
                    }

                    MessageBox.Show(fullReport.ToString(), "Импорт студентов", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (CurrentViewModel is StudentsViewModel) Navigate("Students");
                    else if (CurrentViewModel is GroupsViewModel) Navigate("Groups");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
            // ▲▲▲ Конец импорта ▲▲▲

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
            "Events" => new EventsViewModel(_eventRepository, _dictionaryRepository, _participantRepository, _studentRepository, _teacherRepository, _groupRepository),
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