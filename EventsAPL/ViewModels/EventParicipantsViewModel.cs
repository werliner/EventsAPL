using System.Collections.ObjectModel;
using System.Windows;
using EventsAPL.Base;
using EventsAPL.Data;
using EventsAPL.Models;

namespace EventsAPL.ViewModels;

public class SelectableStudent : ViewModelBase
{
    public Student Student { get; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private DictionaryItem? _selectedResultType;
    public DictionaryItem? SelectedResultType
    {
        get => _selectedResultType;
        set => Set(ref _selectedResultType, value);
    }

    private DictionaryItem? _selectedResultLevel;
    public DictionaryItem? SelectedResultLevel
    {
        get => _selectedResultLevel;
        set => Set(ref _selectedResultLevel, value);
    }

    public SelectableStudent(Student student) => Student = student;
}

public class SelectableTeacher : ViewModelBase
{
    public Teacher Teacher { get; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private DictionaryItem? _selectedResultType;
    public DictionaryItem? SelectedResultType
    {
        get => _selectedResultType;
        set => Set(ref _selectedResultType, value);
    }

    private DictionaryItem? _selectedResultLevel;
    public DictionaryItem? SelectedResultLevel
    {
        get => _selectedResultLevel;
        set => Set(ref _selectedResultLevel, value);
    }

    public SelectableTeacher(Teacher teacher) => Teacher = teacher;
}

public class EventParticipantsViewModel : ViewModelBase
{
    private readonly Event _event;
    private readonly ParticipantRepository _participantRepo;
    private readonly StudentRepository _studentRepo;
    private readonly TeacherRepository _teacherRepo;
    private readonly GroupRepository _groupRepo;
    private readonly DictionaryRepository _dictRepo;

    private List<EventParticipant> _existingParticipants = new();

    public string EventTitle => _event.Title;

    public ObservableCollection<Group> Groups { get; } = new();

    // ВАЖНО: добавлен private set, чтобы мы могли заменять коллекцию целиком одним махом
    public ObservableCollection<SelectableStudent> AvailableStudents { get; private set; } = new();
    public ObservableCollection<SelectableTeacher> AvailableTeachers { get; private set; } = new();

    public ObservableCollection<DictionaryItem> ResultTypes { get; } = new();
    public ObservableCollection<DictionaryItem> ResultLevels { get; } = new();

    private Group? _selectedGroup;
    public Group? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (Set(ref _selectedGroup, value))
                _ = LoadStudentsAsync();
        }
    }

    private bool _isStudentMode = true;
    public bool IsStudentMode
    {
        get => _isStudentMode;
        set
        {
            if (Set(ref _isStudentMode, value)) OnPropertyChanged(nameof(IsTeacherMode));
        }
    }

    public bool IsTeacherMode
    {
        get => !IsStudentMode;
        set => IsStudentMode = !value;
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public Action? CloseAction { get; set; }

    public EventParticipantsViewModel(Event ev, ParticipantRepository partRepo, StudentRepository studRepo,
                                      TeacherRepository teachRepo, GroupRepository groupRepo, DictionaryRepository dictRepo)
    {
        _event = ev;
        _participantRepo = partRepo;
        _studentRepo = studRepo;
        _teacherRepo = teachRepo;
        _groupRepo = groupRepo;
        _dictRepo = dictRepo;

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseAction?.Invoke());

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var rt = await _dictRepo.GetAllAsync("ResultTypes");
            foreach (var i in rt) ResultTypes.Add(i);

            var rl = await _dictRepo.GetAllAsync("ResultLevels");
            foreach (var i in rl) ResultLevels.Add(i);

            _existingParticipants = await _participantRepo.GetByEventAsync(_event.Id);

            // Преподаватели: создаем в памяти, затем отдаем UI
            var teachers = await _teacherRepo.GetAllAsync();
            var newTeachersCollection = new ObservableCollection<SelectableTeacher>();
            foreach (var t in teachers)
            {
                var selectable = new SelectableTeacher(t);
                ApplyTeacherSelection(selectable);
                newTeachersCollection.Add(selectable);
            }
            AvailableTeachers = newTeachersCollection;
            OnPropertyChanged(nameof(AvailableTeachers));

            var groups = await _groupRepo.GetAllAsync();
            foreach (var g in groups) Groups.Add(g);

            if (Groups.Count > 0)
            {
                _selectedGroup = Groups[0];
                OnPropertyChanged(nameof(SelectedGroup));
                await LoadStudentsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка инициализации: {ex.Message}");
        }
    }

    private async Task LoadStudentsAsync()
    {
        if (_selectedGroup == null) return;
        try
        {
            var students = await _studentRepo.GetByGroupAsync(_selectedGroup.Id);
            var newCollection = new ObservableCollection<SelectableStudent>();

            foreach (var s in students)
            {
                var selectable = new SelectableStudent(s);
                ApplyStudentSelection(selectable);
                newCollection.Add(selectable);
            }

            // Отдаем готовую коллекцию UI одним махом (без мерцания)
            AvailableStudents = newCollection;
            OnPropertyChanged(nameof(AvailableStudents));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки студентов: {ex.Message}");
        }
    }

    private void ApplyStudentSelection(SelectableStudent selectable)
    {
        var existing = _existingParticipants.FirstOrDefault(p => p.StudentId == selectable.Student.Id);
        if (existing != null)
        {
            selectable.IsSelected = true;
            selectable.SelectedResultType = ResultTypes.FirstOrDefault(x => x.Id == existing.ResultTypeId);
            selectable.SelectedResultLevel = ResultLevels.FirstOrDefault(x => x.Id == existing.ResultLevelId);
        }
    }

    private void ApplyTeacherSelection(SelectableTeacher selectable)
    {
        var existing = _existingParticipants.FirstOrDefault(p => p.TeacherId == selectable.Teacher.Id);
        if (existing != null)
        {
            selectable.IsSelected = true;
            selectable.SelectedResultType = ResultTypes.FirstOrDefault(x => x.Id == existing.ResultTypeId);
            selectable.SelectedResultLevel = ResultLevels.FirstOrDefault(x => x.Id == existing.ResultLevelId);
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _participantRepo.DeleteAllByEventAsync(_event.Id);

            var selectedStudents = AvailableStudents.Where(x => x.IsSelected).Select(s => new EventParticipant
            {
                EventId = _event.Id,
                StudentId = s.Student.Id,
                ResultTypeId = s.SelectedResultType?.Id,
                ResultLevelId = s.SelectedResultLevel?.Id
            }).ToList();

            var selectedTeachers = AvailableTeachers.Where(x => x.IsSelected).Select(t => new EventParticipant
            {
                EventId = _event.Id,
                TeacherId = t.Teacher.Id,
                ResultTypeId = t.SelectedResultType?.Id,
                ResultLevelId = t.SelectedResultLevel?.Id
            }).ToList();

            var allSelected = selectedStudents.Concat(selectedTeachers).ToList();

            if (allSelected.Any())
            {
                // Отправляем ВСЁ одним пакетом (в 50 раз быстрее)
                await _participantRepo.AddBatchAsync(_event.Id, allSelected);
            }

            MessageBox.Show("Участники сохранены!", "Успех");
            CloseAction?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}");
        }
    }
}