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

    private Group? _selectedGroupFilter;
    public Group? SelectedGroupFilter
    {
        get => _selectedGroupFilter;
        set
        {
            if (Set(ref _selectedGroupFilter, value))
                _ = LoadStudentsAsync();
        }
    }

    private Group? _selectedStudentGroup;
    public Group? SelectedStudentGroup
    {
        get => _selectedStudentGroup;
        set => Set(ref _selectedStudentGroup, value);
    }

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }

    private Student? _selectedStudent;
    public Student? SelectedStudent
    {
        get => _selectedStudent;
        set
        {
            if (Set(ref _selectedStudent, value))
            {
                // Обновляем доступность кнопок при выборе
                OpenEditCommand?.RaiseCanExecuteChanged();
                DeleteCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _editLastName = "";
    public string EditLastName { get => _editLastName; set => Set(ref _editLastName, value); }

    private string _editFirstName = "";
    public string EditFirstName { get => _editFirstName; set => Set(ref _editFirstName, value); }

    private string _editMiddleName = "";
    public string EditMiddleName { get => _editMiddleName; set => Set(ref _editMiddleName, value); }

    public RelayCommand LoadCommand { get; }
    public RelayCommand OpenAddCommand { get; }
    public RelayCommand OpenEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public StudentsViewModel(StudentRepository studentRepo, GroupRepository groupRepo, int? initialGroupId = null)
    {
        _studentRepo = studentRepo;
        _groupRepo = groupRepo;

        LoadCommand = new RelayCommand(async _ => await LoadStudentsAsync());

        OpenAddCommand = new RelayCommand(_ => {
            SelectedStudent = null;
            EditLastName = EditFirstName = EditMiddleName = string.Empty;
            SelectedStudentGroup = SelectedGroupFilter;
            IsEditing = true;
        });

        OpenEditCommand = new RelayCommand(_ => {
            if (SelectedStudent != null)
            {
                EditLastName = SelectedStudent.LastName;
                EditFirstName = SelectedStudent.FirstName;
                EditMiddleName = SelectedStudent.MiddleName;
                SelectedStudentGroup = Groups.FirstOrDefault(g => g.Id == SelectedStudent.GroupId);
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
            var groups = await _groupRepo.GetAllAsync();
            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);

            SelectedGroupFilter = initialGroupId.HasValue
                ? Groups.FirstOrDefault(g => g.Id == initialGroupId.Value)
                : Groups.FirstOrDefault();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка инициализации: {ex.Message}"); }
    }

    private async Task LoadStudentsAsync()
    {
        int groupId = SelectedGroupFilter?.Id ?? 0;
        try
        {
            var list = await _studentRepo.GetByGroupAsync(groupId);
            Students.Clear();
            foreach (var s in list) Students.Add(s);
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditLastName) || SelectedStudentGroup == null)
        {
            MessageBox.Show("Введите фамилию и выберите группу!", "Внимание");
            return;
        }

        try
        {
            if (SelectedStudent == null)
            {
                var newStudent = new Student
                {
                    GroupId = SelectedStudentGroup.Id,
                    LastName = EditLastName,
                    FirstName = EditFirstName,
                    MiddleName = EditMiddleName
                };
                await _studentRepo.AddAsync(newStudent);
            }
            else
            {
                SelectedStudent.LastName = EditLastName;
                SelectedStudent.FirstName = EditFirstName;
                SelectedStudent.MiddleName = EditMiddleName;
                SelectedStudent.GroupId = SelectedStudentGroup.Id;
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