using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;
using EventsAPL.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace EventsAPL.ViewModels;

public class EventsViewModel : ViewModelBase
{
    private readonly EventRepository _eventRepo;
    private readonly DictionaryRepository _dictRepo;
    private readonly ParticipantRepository _participantRepo;
    private readonly StudentRepository _studentRepo;
    private readonly TeacherRepository _teacherRepo;
    private readonly GroupRepository _groupRepo;

    // ИСПРАВЛЕНИЕ 2: Токен отмены для предотвращения race condition
    private CancellationTokenSource? _loadCts;

    private List<Event> _allEvents = new();
    public ObservableCollection<Event> Events { get; } = new();
    public ObservableCollection<DictionaryItem> Levels { get; } = new();
    public ObservableCollection<DictionaryItem> Types { get; } = new();

    // ИСПРАВЛЕНИЕ 3: Индикатор загрузки
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => Set(ref _isLoading, value);
    }

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
                OpenParticipantsCommand?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanEditParticipants));
            }
        }
    }

    public bool CanEditParticipants => SelectedEvent != null && IsEditing;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) ApplyFilter(); }
    }

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

    public RelayCommand LoadCommand { get; }
    public RelayCommand OpenAddCommand { get; }
    public RelayCommand OpenEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand OpenParticipantsCommand { get; }

    public EventsViewModel(EventRepository eventRepo, DictionaryRepository dictRepo,
                           ParticipantRepository participantRepo, StudentRepository studentRepo,
                           TeacherRepository teacherRepo, GroupRepository groupRepo)
    {
        _eventRepo = eventRepo;
        _dictRepo = dictRepo;
        _participantRepo = participantRepo;
        _studentRepo = studentRepo;
        _teacherRepo = teacherRepo;
        _groupRepo = groupRepo;

        LoadCommand = new RelayCommand(async _ => await LoadAsync());

        OpenAddCommand = new RelayCommand(_ => {
            SelectedEvent = null;
            EditTitle = string.Empty;
            EditLevel = Levels.FirstOrDefault();
            EditType = Types.FirstOrDefault();
            EditStartDate = DateTime.Today;
            EditEndDate = null;
            IsEditing = true;
            OnPropertyChanged(nameof(CanEditParticipants));
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
                OnPropertyChanged(nameof(CanEditParticipants));
            }
        }, _ => SelectedEvent != null);

        OpenParticipantsCommand = new RelayCommand(async _ =>
        {
            if (SelectedEvent == null)
            {
                var saved = await SaveAsync(closeModal: false);
                if (!saved) return;
            }

            if (SelectedEvent != null)
            {
                var vm = new EventParticipantsViewModel(SelectedEvent, _participantRepo, _studentRepo, _teacherRepo, _groupRepo, _dictRepo);
                var view = new EventParticipantsView(vm);
                view.Owner = Application.Current.MainWindow;
                view.ShowDialog();
            }
        });

        SaveCommand = new RelayCommand(async _ => await SaveAsync(closeModal: true));

        CancelCommand = new RelayCommand(_ => {
            IsEditing = false;
            OnPropertyChanged(nameof(CanEditParticipants));
        });

        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedEvent != null);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var levels = await _dictRepo.GetAllAsync("EventLevels");
            Levels.Clear();
            foreach (var l in levels) Levels.Add(l);

            var types = await _dictRepo.GetAllAsync("EventTypes");
            Types.Clear();
            foreach (var t in types) Types.Add(t);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка инициализации: {ex.Message}");
        }
    }

    // ИСПРАВЛЕНИЕ 4: Метод загрузки с отменой предыдущих запросов
    private async Task LoadAsync()
    {
        // Отменяем предыдущий запрос, если он ещё идёт
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        try
        {
            IsLoading = true;

            token.ThrowIfCancellationRequested();

            _allEvents = await _eventRepo.GetAllAsync();

            token.ThrowIfCancellationRequested();

            // Парсинг в background thread, чтобы не блокировать UI
            await Task.Run(() => ApplyFilter(), token);
        }
        catch (OperationCanceledException)
        {
            // Запрос был отменён - это нормально
            System.Diagnostics.Debug.WriteLine("[Events] Загрузка отменена");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        }
        finally
        {
            // Снимаем флаг загрузки только если это не отменённый запрос
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allEvents
            : _allEvents.Where(e => e.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        // Обновляем UI в один присест через Dispatcher
        Application.Current.Dispatcher.Invoke(() =>
        {
            Events.Clear();
            foreach (var e in filtered)
                Events.Add(e);
        });
    }

    private async Task<bool> SaveAsync(bool closeModal)
    {
        if (string.IsNullOrWhiteSpace(EditTitle) || EditLevel == null || EditType == null)
        {
            MessageBox.Show("Заполните название, выберите уровень и вид!", "Внимание");
            return false;
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
                await LoadAsync();

                SelectedEvent = Events.FirstOrDefault(e =>
                    e.Title == newEv.Title && e.StartDate == newEv.StartDate);
            }
            else
            {
                SelectedEvent.Title = EditTitle;
                SelectedEvent.EventLevelId = EditLevel.Id;
                SelectedEvent.EventTypeId = EditType.Id;
                SelectedEvent.StartDate = EditStartDate;
                SelectedEvent.EndDate = EditEndDate;

                await _eventRepo.UpdateAsync(SelectedEvent);
                await LoadAsync();
            }

            if (closeModal)
            {
                IsEditing = false;
                OnPropertyChanged(nameof(CanEditParticipants));
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            return false;
        }
    }

    private async Task DeleteAsync()
    {
        var eventToDelete = SelectedEvent;
        if (eventToDelete == null) return;

        if (MessageBox.Show($"Удалить мероприятие '{eventToDelete.Title}'?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try
            {
                await _eventRepo.DeleteAsync(eventToDelete.Id);
                SelectedEvent = null;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}