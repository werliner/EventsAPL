using System.Collections.ObjectModel;
using System.Windows;
using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;

namespace EventsAPL.ViewModels;

public class EventsViewModel : ViewModelBase
{
    private readonly EventRepository _eventRepo;
    private readonly DictionaryRepository _dictRepo;

    public ObservableCollection<Event> Events { get; } = new();
    public ObservableCollection<DictionaryItem> Levels { get; } = new();
    public ObservableCollection<DictionaryItem> Types { get; } = new();

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }

    private Event? _selectedEvent;
    public Event? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (Set(ref _selectedEvent, value))
            {
                OpenEditCommand?.RaiseCanExecuteChanged();
                DeleteCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    // Поля для модального окна
    private string _editTitle = string.Empty;
    public string EditTitle { get => _editTitle; set => Set(ref _editTitle, value); }

    private DictionaryItem? _editLevel;
    public DictionaryItem? EditLevel { get => _editLevel; set => Set(ref _editLevel, value); }

    private DictionaryItem? _editType;
    public DictionaryItem? EditType { get => _editType; set => Set(ref _editType, value); }

    private DateTime _editStartDate = DateTime.Today;
    public DateTime EditStartDate { get => _editStartDate; set => Set(ref _editStartDate, value); }

    private DateTime? _editEndDate;
    public DateTime? EditEndDate { get => _editEndDate; set => Set(ref _editEndDate, value); }

    // Команды
    public RelayCommand LoadCommand { get; }
    public RelayCommand OpenAddCommand { get; }
    public RelayCommand OpenEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public EventsViewModel(EventRepository eventRepo, DictionaryRepository dictRepo)
    {
        _eventRepo = eventRepo;
        _dictRepo = dictRepo;

        LoadCommand = new RelayCommand(async _ => await LoadAsync());

        OpenAddCommand = new RelayCommand(_ => {
            SelectedEvent = null;
            EditTitle = string.Empty;
            EditLevel = Levels.FirstOrDefault();
            EditType = Types.FirstOrDefault();
            EditStartDate = DateTime.Today;
            EditEndDate = null;
            IsEditing = true;
        });

        OpenEditCommand = new RelayCommand(_ => {
            if (SelectedEvent != null)
            {
                EditTitle = SelectedEvent.Title;
                EditLevel = Levels.FirstOrDefault(x => x.Id == SelectedEvent.EventLevelId);
                EditType = Types.FirstOrDefault(x => x.Id == SelectedEvent.EventTypeId);
                EditStartDate = SelectedEvent.StartDate;
                EditEndDate = SelectedEvent.EndDate;
                IsEditing = true;
            }
        }, _ => SelectedEvent != null);

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => IsEditing = false);
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedEvent != null);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Грузим справочники
            var levels = await _dictRepo.GetAllAsync("EventLevels");
            Levels.Clear(); foreach (var l in levels) Levels.Add(l);

            var types = await _dictRepo.GetAllAsync("EventTypes");
            Types.Clear(); foreach (var t in types) Types.Add(t);

            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка инициализации: {ex.Message}"); }
    }

    private async Task LoadAsync()
    {
        try
        {
            var list = await _eventRepo.GetAllAsync();
            Events.Clear();
            foreach (var e in list) Events.Add(e);
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle) || EditLevel == null || EditType == null)
        {
            MessageBox.Show("Заполните название, выберите уровень и вид!", "Внимание");
            return;
        }

        try
        {
            if (SelectedEvent == null)
            {
                var newEv = new Event
                {
                    Title = EditTitle,
                    EventLevelId = EditLevel.Id,
                    EventTypeId = EditType.Id,
                    StartDate = EditStartDate,
                    EndDate = EditEndDate
                };
                await _eventRepo.AddAsync(newEv);
            }
            else
            {
                SelectedEvent.Title = EditTitle;
                SelectedEvent.EventLevelId = EditLevel.Id;
                SelectedEvent.EventTypeId = EditType.Id;
                SelectedEvent.StartDate = EditStartDate;
                SelectedEvent.EndDate = EditEndDate;
                await _eventRepo.UpdateAsync(SelectedEvent);
            }

            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка сохранения: {ex.Message}"); }
    }

    private async Task DeleteAsync()
    {
        if (SelectedEvent == null) return;
        if (MessageBox.Show($"Удалить мероприятие '{SelectedEvent.Title}'?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            try
            {
                await _eventRepo.DeleteAsync(SelectedEvent.Id);
                await LoadAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка удаления: {ex.Message}"); }
        }
    }
}