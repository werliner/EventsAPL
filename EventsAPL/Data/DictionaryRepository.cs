using System.Text.Json;
using EventsAPL.Models;

namespace EventsAPL.Data;

public class DictionaryRepository
{
    private readonly TursoClient _client;

    public DictionaryRepository(TursoClient client)
    {
        _client = client;
    }

    public async Task<List<DictionaryItem>> GetAllAsync(string tableName)
    {
        string sql = $"SELECT Id, Title FROM {tableName} ORDER BY Title";
        var jsonResponse = await _client.ExecuteAsync(sql);
        return ParseItems(jsonResponse);
    }

    public async Task AddAsync(string tableName, string title)
    {
        string sql = $"INSERT INTO {tableName} (Title) VALUES ('{Escape(title)}')";
        await _client.ExecuteAsync(sql);
    }

    public async Task UpdateAsync(string tableName, int id, string title)
    {
        string sql = $"UPDATE {tableName} SET Title = '{Escape(title)}' WHERE Id = {id}";
        await _client.ExecuteAsync(sql);
    }

    public async Task DeleteAsync(string tableName, int id)
    {
        await _client.ExecuteAsync($"DELETE FROM {tableName} WHERE Id = {id}");
    }

    private List<DictionaryItem> ParseItems(string jsonResponse)
    {
        var items = new List<DictionaryItem>();
        using var document = JsonDocument.Parse(jsonResponse);

        if (!document.RootElement.TryGetProperty("results", out var results)) return items;

        var rows = results[0].GetProperty("response").GetProperty("result").GetProperty("rows");

        foreach (var row in rows.EnumerateArray())
        {
            items.Add(new DictionaryItem
            {
                Id = int.Parse(row[0].GetProperty("value").GetString()!),
                Title = row[1].GetProperty("value").GetString()!
            });
        }
        return items;
    }

    private string Escape(string text) => text.Replace("'", "''");
}