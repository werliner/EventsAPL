namespace EventsAPL.Models;

public class ExportRow
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string LevelTitle { get; set; } = string.Empty;
    public string TypeTitle { get; set; } = string.Empty;

    // Участник (Студент)
    public int? StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string GroupNumber { get; set; } = string.Empty;

    // Руководитель
    public string LeaderName { get; set; } = string.Empty;
    public string LeaderRole { get; set; } = string.Empty; // Номинация/Тема работы

    // Результат
    public string ResultText { get; set; } = string.Empty;
}