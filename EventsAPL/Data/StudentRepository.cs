using System.Text.Json;
using EventsAPL.Models;

namespace EventsAPL.Data;

public class StudentRepository
{
    private readonly TursoClient _client;

    public StudentRepository(TursoClient client)
    {
        _client = client;
    }

    public async Task<List<Student>> GetByGroupAsync(int groupId)
    {
        string sql = groupId > 0
            ? $"SELECT Id, GroupId, LastName, FirstName, MiddleName FROM Students WHERE GroupId = {groupId} ORDER BY LastName"
            : "SELECT Id, GroupId, LastName, FirstName, MiddleName FROM Students ORDER BY LastName";

        var jsonResponse = await _client.ExecuteAsync(sql);
        return ParseStudents(jsonResponse);
    }

    public async Task AddAsync(Student student)
    {
        string sql = $"INSERT INTO Students (GroupId, LastName, FirstName, MiddleName) VALUES ({student.GroupId}, '{Escape(student.LastName)}', '{Escape(student.FirstName)}', '{Escape(student.MiddleName)}')";
        await _client.ExecuteAsync(sql);
    }

    public async Task UpdateAsync(Student student)
    {
        string sql = $"UPDATE Students SET GroupId = {student.GroupId}, LastName = '{Escape(student.LastName)}', FirstName = '{Escape(student.FirstName)}', MiddleName = '{Escape(student.MiddleName)}' WHERE Id = {student.Id}";
        await _client.ExecuteAsync(sql);
    }

    public async Task DeleteAsync(int id)
    {
        await _client.ExecuteAsync($"DELETE FROM Students WHERE Id = {id}");
    }

    // Вспомогательный метод для парсинга JSON от Turso
    private List<Student> ParseStudents(string jsonResponse)
    {
        var students = new List<Student>();
        using var document = JsonDocument.Parse(jsonResponse);

        // Проверка на случай, если пришел пустой ответ
        if (!document.RootElement.TryGetProperty("results", out var results)) return students;

        var rows = results[0].GetProperty("response").GetProperty("result").GetProperty("rows");

        foreach (var row in rows.EnumerateArray())
        {
            students.Add(new Student
            {
                Id = int.Parse(row[0].GetProperty("value").GetString()!),
                GroupId = int.Parse(row[1].GetProperty("value").GetString()!),
                LastName = row[2].GetProperty("value").GetString()!,
                FirstName = row[3].GetProperty("value").GetString()!,
                MiddleName = row[4].GetProperty("value").GetString()!
            });
        }
        return students;
    }

    private string Escape(string text) => text.Replace("'", "''");
}