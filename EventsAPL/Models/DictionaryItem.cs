namespace EventsAPL.Models;

public class DictionaryItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public override string ToString() => Title;
}