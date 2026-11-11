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
    public DictionaryItem? SelectedResultType { get; set; }
    public DictionaryItem? SelectedResultLevel { get; set; }
    public SelectableStudent(Student student) => Student = student;
}

public class SelectableTeacher : ViewModelBase
{
    public Teacher Teacher { get; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public DictionaryItem? SelectedResultType { get; set; }
    public DictionaryItem? SelectedResultLevel { get; set; }
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

    public string EventTitle => _event.Title;

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<SelectableStudent> AvailableStudents { get; } = new();
    public ObservableCollection<SelectableTeacher> AvailableTeachers { get; } = new();
    public ObservableCollection<DictionaryItem> ResultTypes { get; } = new();
    public ObservableCollection<DictionaryItem> ResultLevels { get; } = new();

    private Group? _selectedGroup;
    public Group? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (Set(ref _selectedGroup, value)) _ = LoadStudentsAsync();
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

            var groups = await _groupRepo.GetAllAsync();
            foreach (var g in groups) Groups.Add(g);
            SelectedGroup = Groups.FirstOrDefault();

            var teachers = await _teacherRepo.GetAllAsync();
            foreach (var t in teachers) AvailableTeachers.Add(new SelectableTeacher(t));

            var existing = await _participantRepo.GetByEventAsync(_event.Id);

            foreach (var p in existing)
            {
                if (p.StudentId.HasValue)
                {
                    var st = AvailableStudents.FirstOrDefault(x => x.Student.Id == p.StudentId.Value);
                    if (st != null)
                    {
                        st.IsSelected = true;
                        st.SelectedResultType = ResultTypes.FirstOrDefault(x => x.Id == p.ResultTypeId);
                        st.SelectedResultLevel = ResultLevels.FirstOrDefault(x => x.Id == p.ResultLevelId);
                    }
                }
                else if (p.TeacherId.HasValue)
                {
                    var t = AvailableTeachers.FirstOrDefault(x => x.Teacher.Id == p.TeacherId.Value);
                    if (t != null)
                    {
                        t.IsSelected = true;
                        t.SelectedResultType = ResultTypes.FirstOrDefault(x => x.Id == p.ResultTypeId);
                        t.SelectedResultLevel = ResultLevels.FirstOrDefault(x => x.Id == p.ResultLevelId);
                    }
                }
            }
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка инициализации: {ex.Message}"); }
    }

    private async Task LoadStudentsAsync()
    {
        if (SelectedGroup == null) return;
        try
        {
            var students = await _studentRepo.GetByGroupAsync(SelectedGroup.Id);
            AvailableStudents.Clear();
            foreach (var s in students) AvailableStudents.Add(new SelectableStudent(s));
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка загрузки студентов: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _participantRepo.DeleteAllByEventAsync(_event.Id);

            foreach (var s in AvailableStudents.Where(x => x.IsSelected))
            {
                await _participantRepo.AddAsync(new EventParticipant
                {
                    EventId = _event.Id,
                    StudentId = s.Student.Id,
                    ResultTypeId = s.SelectedResultType?.Id,
                    ResultLevelId = s.SelectedResultLevel?.Id
                });
            }

            foreach (var t in AvailableTeachers.Where(x => x.IsSelected))
            {
                await _participantRepo.AddAsync(new EventParticipant
                {
                    EventId = _event.Id,
                    TeacherId = t.Teacher.Id,
                    ResultTypeId = t.SelectedResultType?.Id,
                    ResultLevelId = t.SelectedResultLevel?.Id
                });
            }

            MessageBox.Show("Участники сохранены!", "Успех");
            CloseAction?.Invoke();
        }
        catch (Exception ex) { MessageBox.Show($"Ошибка сохранения: {ex.Message}"); }
    }
}