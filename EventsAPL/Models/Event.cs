namespace EventsAPL.Models;

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int EventLevelId { get; set; }
    public int EventTypeId { get; set; }

    // Даты храним как DateTime в коде, а в базу уходят как строка
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }

    // Эти поля мы заполняем из JOIN'а (чтобы выводить в интерфейсе)
    public string EventLevelTitle { get; set; } = string.Empty;
    public string EventTypeTitle { get; set; } = string.Empty;

    public override string ToString() => Title;
}