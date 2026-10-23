using System.Text.Json;
using EventsAPL.Models;

namespace EventsAPL.Data;

public class EventRepository
{
    private readonly TursoClient _client;

    public EventRepository(TursoClient client)
    {
        _client = client;
    }

    public async Task<List<Event>> GetAllAsync()
    {
        // Берем все мероприятия и присоединяем к ним названия уровней и видов
        string sql = @"
            SELECT e.Id, e.Title, e.EventLevelId, e.EventTypeId, e.StartDate, e.EndDate, 
                   l.Title AS LevelTitle, t.Title AS TypeTitle 
            FROM Events e 
            LEFT JOIN EventLevels l ON e.EventLevelId = l.Id 
            LEFT JOIN EventTypes t ON e.EventTypeId = t.Id 
            ORDER BY e.StartDate DESC";

        var jsonResponse = await _client.ExecuteAsync(sql);
        return ParseEvents(jsonResponse);
    }

    public async Task AddAsync(Event ev)
    {
        string endDateStr = ev.EndDate.HasValue ? $"'{ev.EndDate.Value:yyyy-MM-dd}'" : "NULL";
        string sql = $"INSERT INTO Events (Title, EventLevelId, EventTypeId, StartDate, EndDate) VALUES ('{Escape(ev.Title)}', {ev.EventLevelId}, {ev.EventTypeId}, '{ev.StartDate:yyyy-MM-dd}', {endDateStr})";
        await _client.ExecuteAsync(sql);
    }

    public async Task UpdateAsync(Event ev)
    {
        string endDateStr = ev.EndDate.HasValue ? $"'{ev.EndDate.Value:yyyy-MM-dd}'" : "NULL";
        string sql = $"UPDATE Events SET Title = '{Escape(ev.Title)}', EventLevelId = {ev.EventLevelId}, EventTypeId = {ev.EventTypeId}, StartDate = '{ev.StartDate:yyyy-MM-dd}', EndDate = {endDateStr} WHERE Id = {ev.Id}";
        await _client.ExecuteAsync(sql);
    }

    public async Task DeleteAsync(int id)
    {
        await _client.ExecuteAsync($"DELETE FROM Events WHERE Id = {id}");
    }

    private List<Event> ParseEvents(string jsonResponse)
    {
        var events = new List<Event>();
        using var document = JsonDocument.Parse(jsonResponse);

        if (!document.RootElement.TryGetProperty("results", out var results)) return events;

        var rows = results[0].GetProperty("response").GetProperty("result").GetProperty("rows");

        foreach (var row in rows.EnumerateArray())
        {
            var ev = new Event
            {
                Id = int.Parse(row[0].GetProperty("value").GetString()!),
                Title = row[1].GetProperty("value").GetString()!,
                EventLevelId = int.Parse(row[2].GetProperty("value").GetString()!),
                EventTypeId = int.Parse(row[3].GetProperty("value").GetString()!),
                StartDate = DateTime.Parse(row[4].GetProperty("value").GetString()!),
                EventLevelTitle = row[6].TryGetProperty("value", out var l) ? l.GetString()! : "Не указан",
                EventTypeTitle = row[7].TryGetProperty("value", out var t) ? t.GetString()! : "Не указан"
            };

            // Обработка EndDate (может быть NULL)
            if (row[5].TryGetProperty("value", out var endVal) && endVal.ValueKind != JsonValueKind.Null)
                ev.EndDate = DateTime.Parse(endVal.GetString()!);

            events.Add(ev);
        }
        return events;
    }

    private string Escape(string text) => text.Replace("'", "''");
}