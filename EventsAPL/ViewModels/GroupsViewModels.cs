using System.Collections.ObjectModel;
using System.Windows;
using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;

namespace EventsAPL.ViewModels;

public class GroupsViewModel : ViewModelBase
{
    private readonly GroupRepository _repository;
    private readonly Action<Group> _navigateToStudents;

    public ObservableCollection<Group> Groups { get; } = new();

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }

    private Group? _selectedGroup;
    public Group? SelectedGroup { get => _selectedGroup; set => Set(ref _selectedGroup, value); }

    private string _editNumber = string.Empty;
    public string EditNumber { get => _editNumber; set => Set(ref _editNumber, value); }

    private string _editTitle = string.Empty;
    public string EditTitle { get => _editTitle; set => Set(ref _editTitle, value); }

    public RelayCommand LoadCommand { get; }
    public RelayCommand OpenAddCommand { get; }
    public RelayCommand OpenEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ViewStudentsCommand { get; }

    // ВОТ ЗДЕСЬ ДОЛЖНО БЫТЬ ДВА АРГУМЕНТА!
    public GroupsViewModel(GroupRepository repository, Action<Group> navigateToStudents)
    {
        _repository = repository;
        _navigateToStudents = navigateToStudents;

        LoadCommand = new RelayCommand(async _ => await LoadAsync());

        OpenAddCommand = new RelayCommand(_ =>
        {
            SelectedGroup = null;
            EditNumber = string.Empty;
            EditTitle = string.Empty;
            IsEditing = true;
        });

        OpenEditCommand = new RelayCommand(_ =>
        {
            if (SelectedGroup != null)
            {
                EditNumber = SelectedGroup.Number;
                EditTitle = SelectedGroup.Title;
                IsEditing = true;
            }
        }, _ => SelectedGroup != null);

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => IsEditing = false);

        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedGroup != null);

        ViewStudentsCommand = new RelayCommand(_ =>
        {
            if (SelectedGroup != null) _navigateToStudents(SelectedGroup);
        }, _ => SelectedGroup != null);

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var list = await _repository.GetAllAsync();
            Groups.Clear();
            foreach (var g in list) Groups.Add(g);
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditNumber) || string.IsNullOrWhiteSpace(EditTitle))
        {
            MessageBox.Show("Заполните номер и название группы.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (SelectedGroup == null)
            {
                var newGroup = new Group { Number = EditNumber, Title = EditTitle };
                await _repository.AddAsync(newGroup);
            }
            else
            {
                SelectedGroup.Number = EditNumber;
                SelectedGroup.Title = EditTitle;
                await _repository.UpdateAsync(SelectedGroup);
            }

            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка сохранения: {ex.Message}"); }
    }

    private async Task DeleteAsync()
    {
        if (SelectedGroup == null) return;

        var result = MessageBox.Show($"Удалить группу '{SelectedGroup.Number}'?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _repository.DeleteAsync(SelectedGroup.Id);
                await LoadAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка удаления: {ex.Message}"); }
        }
    }
}