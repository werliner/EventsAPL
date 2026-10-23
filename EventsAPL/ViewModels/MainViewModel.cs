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
    private readonly StudentRepository _studentRepository;

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

    public MainViewModel()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        var config = JsonDocument.Parse(File.ReadAllText(configPath)).RootElement.GetProperty("Turso");

        string url = config.GetProperty("Url").GetString()!;
        string token = config.GetProperty("Token").GetString()!;

        var client = new TursoClient(url, token);
        _groupRepository = new GroupRepository(client);
        _studentRepository = new StudentRepository(client);

        NavigateCommand = new RelayCommand(param => Navigate(param as string));
        LoginCommand = new RelayCommand(_ => MessageBox.Show("Окно авторизации появится позже"));

        Navigate("Events");
    }

    private void Navigate(string? key)
    {
        CurrentViewModel = key switch
        {
            "Groups" => new GroupsViewModel(_groupRepository, NavigateToStudents),
            "Students" => new StudentsViewModel(_studentRepository, _groupRepository),
            _ => new PlaceholderViewModel("Раздел в разработке")
        };
    }

    private void NavigateToStudents(Group group)
    {
        CurrentViewModel = new StudentsViewModel(_studentRepository, _groupRepository, group.Id);
    }
}