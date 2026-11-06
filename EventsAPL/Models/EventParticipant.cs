namespace EventsAPL.Models;

public class EventParticipant
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }
    public int? ResultTypeId { get; set; }
    public int? ResultLevelId { get; set; }

    public string ParticipantName { get; set; } = string.Empty;
    public string ResultDisplay { get; set; } = string.Empty;
}