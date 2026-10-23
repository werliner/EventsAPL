namespace EventsAPL.Models;

public class Group
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;


    public override string ToString()
    {
        return Number;
    }
}