using System.Collections.ObjectModel;
using System.Windows;
using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;

namespace EventsAPL.ViewModels;

public class StudentsViewModel : ViewModelBase
{
    private readonly StudentRepository _studentRepo;
    private readonly GroupRepository _groupRepo;

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<Student> Students { get; } = new();

    private Group? _selectedGroup;
    public Group? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (Set(ref _selectedGroup, value))
                _ = LoadStudentsAsync(); // При смене группы сразу грузим её студентов
        }
    }

    // Свойства для модального окна
    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    private Student? _selectedStudent;
    public Student? SelectedStudent
    {
        get => _selectedStudent;
        set => Set(ref _selectedStudent, value);
    }

    private string _editLastName = "";
    public string EditLastName { get => _editLastName; set => Set(ref _editLastName, value); }

    private string _editFirstName = "";
    public string EditFirstName { get => _editFirstName; set => Set(ref _editFirstName, value); }

    private string _editMiddleName = "";
    public string EditMiddleName { get => _editMiddleName; set => Set(ref _editMiddleName, value); }

    // Команды
    public RelayCommand LoadCommand { get; }
    public RelayCommand OpenAddCommand { get; }
    public RelayCommand OpenEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeleteCommand { get; }

    // Конструктор принимает репозитории и (опционально) ID группы для фильтра
    public StudentsViewModel(StudentRepository studentRepo, GroupRepository groupRepo, int? initialGroupId = null)
    {
        _studentRepo = studentRepo;
        _groupRepo = groupRepo;

        LoadCommand = new RelayCommand(async _ => await LoadAllAsync());

        OpenAddCommand = new RelayCommand(_ => {
            SelectedStudent = null;
            EditLastName = EditFirstName = EditMiddleName = "";
            IsEditing = true;
        });

        OpenEditCommand = new RelayCommand(_ => {
            if (SelectedStudent != null)
            {
                EditLastName = SelectedStudent.LastName;
                EditFirstName = SelectedStudent.FirstName;
                EditMiddleName = SelectedStudent.MiddleName;
                IsEditing = true;
            }
        }, _ => SelectedStudent != null);

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => IsEditing = false);
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedStudent != null);

        _ = InitializeAsync(initialGroupId);
    }

    private async Task InitializeAsync(int? initialGroupId)
    {
        try
        {
            // Загружаем группы для ComboBox
            var groups = await _groupRepo.GetAllAsync();
            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);

            // Устанавливаем стартовую группу (если перешли из экрана Групп)
            SelectedGroup = initialGroupId.HasValue
                ? Groups.FirstOrDefault(g => g.Id == initialGroupId.Value)
                : Groups.FirstOrDefault(); // Если не передали, выбираем первую
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка инициализации: {ex.Message}");
        }
    }

    private async Task LoadAllAsync()
    {
        if (SelectedGroup == null) return;
        try
        {
            var list = await _studentRepo.GetByGroupAsync(SelectedGroup.Id);
            Students.Clear();
            foreach (var s in list) Students.Add(s);
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки: {ex.Message}"); }
    }

    private async Task LoadStudentsAsync()
    {
        await LoadAllAsync();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditLastName) || SelectedGroup == null)
        {
            MessageBox.Show("Введите фамилию и выберите группу!", "Внимание");
            return;
        }

        try
        {
            if (SelectedStudent == null) // Создание
            {
                var newStudent = new Student
                {
                    GroupId = SelectedGroup.Id,
                    LastName = EditLastName,
                    FirstName = EditFirstName,
                    MiddleName = EditMiddleName
                };
                await _studentRepo.AddAsync(newStudent);
            }
            else // Редактирование
            {
                SelectedStudent.LastName = EditLastName;
                SelectedStudent.FirstName = EditFirstName;
                SelectedStudent.MiddleName = EditMiddleName;
                // GroupId пока оставим тот же, чтобы не усложнять
                await _studentRepo.UpdateAsync(SelectedStudent);
            }

            IsEditing = false;
            await LoadStudentsAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка сохранения: {ex.Message}"); }
    }

    private async Task DeleteAsync()
    {
        if (SelectedStudent == null) return;
        if (MessageBox.Show($"Удалить студента '{SelectedStudent.FullName}'?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            try
            {
                await _studentRepo.DeleteAsync(SelectedStudent.Id);
                await LoadStudentsAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка удаления: {ex.Message}"); }
        }
    }
}