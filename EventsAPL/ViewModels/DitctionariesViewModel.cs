using System.Collections.ObjectModel;
using System.Windows;
using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;

namespace EventsAPL.ViewModels;

public class DictionariesViewModel : ViewModelBase
{
    private readonly DictionaryRepository _repository;

    // Массив с названиями таблиц в базе. Индекс совпадает с индексом вкладки.
    private readonly string[] _tableNames = { "EventLevels", "EventTypes", "ResultTypes", "ResultLevels" };

    public ObservableCollection<DictionaryItem> Items { get; } = new();

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (Set(ref _selectedTabIndex, value))
                _ = LoadAsync(); // При смене вкладки загружаем новую таблицу
        }
    }

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }

    private DictionaryItem? _selectedItem;
    public DictionaryItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (Set(ref _selectedItem, value))
            {
                OpenEditCommand?.RaiseCanExecuteChanged();
                DeleteCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _editTitle = string.Empty;
    public string EditTitle { get => _editTitle; set => Set(ref _editTitle, value); }

    public RelayCommand LoadCommand { get; }
    public RelayCommand OpenAddCommand { get; }
    public RelayCommand OpenEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public DictionariesViewModel(DictionaryRepository repository)
    {
        _repository = repository;

        LoadCommand = new RelayCommand(async _ => await LoadAsync());

        OpenAddCommand = new RelayCommand(_ => {
            SelectedItem = null;
            EditTitle = string.Empty;
            IsEditing = true;
        });

        OpenEditCommand = new RelayCommand(_ => {
            if (SelectedItem != null)
            {
                EditTitle = SelectedItem.Title;
                IsEditing = true;
            }
        }, _ => SelectedItem != null);

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => IsEditing = false);
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedItem != null);

        _ = LoadAsync();
    }

    // Получаем имя текущей таблицы на основе выбранной вкладки
    private string CurrentTable => _tableNames[SelectedTabIndex];

    private async Task LoadAsync()
    {
        try
        {
            var list = await _repository.GetAllAsync(CurrentTable);
            Items.Clear();
            foreach (var i in list) Items.Add(i);
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            MessageBox.Show("Введите название.", "Внимание");
            return;
        }

        try
        {
            if (SelectedItem == null)
                await _repository.AddAsync(CurrentTable, EditTitle);
            else
                await _repository.UpdateAsync(CurrentTable, SelectedItem.Id, EditTitle);

            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка сохранения: {ex.Message}"); }
    }

    private async Task DeleteAsync()
    {
        if (SelectedItem == null) return;
        if (MessageBox.Show($"Удалить '{SelectedItem.Title}'?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            try
            {
                await _repository.DeleteAsync(CurrentTable, SelectedItem.Id);
                await LoadAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка удаления: {ex.Message}"); }
        }
    }
}