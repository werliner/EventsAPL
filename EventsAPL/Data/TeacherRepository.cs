using System.Text.Json;
using EventsAPL.Models;

namespace EventsAPL.Data;

public class TeacherRepository
{
    private readonly TursoClient _client;

    public TeacherRepository(TursoClient client)
    {
        _client = client;
    }

    public async Task<List<Teacher>> GetAllAsync()
    {
        string sql = "SELECT Id, LastName, FirstName, MiddleName FROM Teachers ORDER BY LastName";
        var jsonResponse = await _client.ExecuteAsync(sql);
        return ParseTeachers(jsonResponse);
    }

    public async Task AddAsync(Teacher teacher)
    {
        string sql = $"INSERT INTO Teachers (LastName, FirstName, MiddleName) VALUES ('{Escape(teacher.LastName)}', '{Escape(teacher.FirstName)}', '{Escape(teacher.MiddleName)}')";
        await _client.ExecuteAsync(sql);
    }

    public async Task UpdateAsync(Teacher teacher)
    {
        string sql = $"UPDATE Teachers SET LastName = '{Escape(teacher.LastName)}', FirstName = '{Escape(teacher.FirstName)}', MiddleName = '{Escape(teacher.MiddleName)}' WHERE Id = {teacher.Id}";
        await _client.ExecuteAsync(sql);
    }

    public async Task DeleteAsync(int id)
    {
        await _client.ExecuteAsync($"DELETE FROM Teachers WHERE Id = {id}");
    }

    private List<Teacher> ParseTeachers(string jsonResponse)
    {
        var teachers = new List<Teacher>();
        using var document = JsonDocument.Parse(jsonResponse);

        if (!document.RootElement.TryGetProperty("results", out var results)) return teachers;

        var rows = results[0].GetProperty("response").GetProperty("result").GetProperty("rows");

        foreach (var row in rows.EnumerateArray())
        {
            teachers.Add(new Teacher
            {
                Id = int.Parse(row[0].GetProperty("value").GetString()!),
                LastName = row[1].GetProperty("value").GetString()!,
                FirstName = row[2].GetProperty("value").GetString()!,
                MiddleName = row[3].GetProperty("value").GetString()!
            });
        }
        return teachers;
    }

    private string Escape(string text) => text.Replace("'", "''");
}