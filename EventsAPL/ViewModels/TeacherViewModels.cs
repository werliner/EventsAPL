using System.Collections.ObjectModel;
using System.Windows;
using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;

namespace EventsAPL.ViewModels;

public class TeachersViewModel : ViewModelBase
{
    private readonly TeacherRepository _repository;

    private List<Teacher> _allTeachers = new();

    public ObservableCollection<Teacher> Teachers { get; } = new();

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }

    private Teacher? _selectedTeacher;
    public Teacher? SelectedTeacher
    {
        get => _selectedTeacher;
        set
        {
            if (Set(ref _selectedTeacher, value))
            {
                OpenEditCommand?.RaiseCanExecuteChanged();
                DeleteCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) ApplyFilter(); }
    }

    private string _editLastName = string.Empty;
    public string EditLastName { get => _editLastName; set => Set(ref _editLastName, value); }

    private string _editFirstName = string.Empty;
    public string EditFirstName { get => _editFirstName; set => Set(ref _editFirstName, value); }

    private string _editMiddleName = string.Empty;
    public string EditMiddleName { get => _editMiddleName; set => Set(ref _editMiddleName, value); }

    public RelayCommand LoadCommand { get; }
    public RelayCommand OpenAddCommand { get; }
    public RelayCommand OpenEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public TeachersViewModel(TeacherRepository repository)
    {
        _repository = repository;

        LoadCommand = new RelayCommand(async _ => await LoadAsync());

        OpenAddCommand = new RelayCommand(_ => {
            SelectedTeacher = null;
            EditLastName = EditFirstName = EditMiddleName = string.Empty;
            IsEditing = true;
        });

        OpenEditCommand = new RelayCommand(_ => {
            if (SelectedTeacher != null)
            {
                EditLastName = SelectedTeacher.LastName;
                EditFirstName = SelectedTeacher.FirstName;
                EditMiddleName = SelectedTeacher.MiddleName;
                IsEditing = true;
            }
        }, _ => SelectedTeacher != null);

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => IsEditing = false);
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedTeacher != null);

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _allTeachers = await _repository.GetAllAsync();
            ApplyFilter();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки: {ex.Message}"); }
    }

    private void ApplyFilter()
    {
        Teachers.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allTeachers
            : _allTeachers.Where(t => t.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (var t in filtered) Teachers.Add(t);
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditLastName) || string.IsNullOrWhiteSpace(EditFirstName))
        {
            MessageBox.Show("Заполните хотя бы фамилию и имя преподавателя.", "Внимание");
            return;
        }

        try
        {
            if (SelectedTeacher == null)
            {
                var newTeacher = new Teacher { LastName = EditLastName, FirstName = EditFirstName, MiddleName = EditMiddleName };
                await _repository.AddAsync(newTeacher);
            }
            else
            {
                SelectedTeacher.LastName = EditLastName;
                SelectedTeacher.FirstName = EditFirstName;
                SelectedTeacher.MiddleName = EditMiddleName;
                await _repository.UpdateAsync(SelectedTeacher);
            }

            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка сохранения: {ex.Message}"); }
    }

    private async Task DeleteAsync()
    {
        if (SelectedTeacher == null) return;
        if (MessageBox.Show($"Удалить преподавателя '{SelectedTeacher.FullName}'?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            try
            {
                await _repository.DeleteAsync(SelectedTeacher.Id);
                await LoadAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка удаления: {ex.Message}"); }
        }
    }
}