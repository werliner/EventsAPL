namespace EventsAPL.Models;

public class Student
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;

   
    public string FullName => $"{LastName} {FirstName} {MiddleName}".Trim();
}