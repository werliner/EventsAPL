using System.Text.Json;
using System.Text.Json.Nodes;
using EventsAPL.Models;

namespace EventsAPL.Data;

public class GroupRepository
{
    private readonly TursoClient _client;

    // В конструктор передаем клиент, чтобы использовать его для запросов
    public GroupRepository(TursoClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Получает все группы из базы данных.
    /// </summary>
    public async Task<List<Group>> GetAllAsync()
    {
        // 1. Отправляем SQL-запрос
        var jsonResponse = await _client.ExecuteAsync("SELECT Id, Number, Title FROM Groups ORDER BY Number");

        // 2. Парсим ответ
        var groups = new List<Group>();

        // Turso возвращает сложный JSON. Мы вытаскиваем из него массив строк (rows).
        // Структура: results[0] -> response -> result -> rows
        using var document = JsonDocument.Parse(jsonResponse);
        var rows = document.RootElement
            .GetProperty("results")[0]
            .GetProperty("response")
            .GetProperty("result")
            .GetProperty("rows");

        // 3. Проходимся по каждой строке и создаем объект Group
        foreach (var row in rows.EnumerateArray())
        {
            // Turso возвращает данные в виде массива: [ {value: "1"}, {value: "КМТ-22"}, ... ]
            var group = new Group
            {
                Id = int.Parse(row[0].GetProperty("value").GetString()!),
                Number = row[1].GetProperty("value").GetString()!,
                Title = row[2].GetProperty("value").GetString()!
            };
            groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// Добавляет новую группу в базу данных.
    /// </summary>
    public async Task AddAsync(Group group)
    {
        // Экранируем одинарные кавычки, чтобы не сломать SQL-запрос (защита от инъекций)
        string safeNumber = group.Number.Replace("'", "''");
        string safeTitle = group.Title.Replace("'", "''");

        string sql = $"INSERT INTO Groups (Number, Title) VALUES ('{safeNumber}', '{safeTitle}')";

        await _client.ExecuteAsync(sql);
    }

    /// <summary>
    /// Обновляет существующую группу.
    /// </summary>
    public async Task UpdateAsync(Group group)
    {
        string safeNumber = group.Number.Replace("'", "''");
        string safeTitle = group.Title.Replace("'", "''");

        string sql = $"UPDATE Groups SET Number = '{safeNumber}', Title = '{safeTitle}' WHERE Id = {group.Id}";

        await _client.ExecuteAsync(sql);
    }

    /// <summary>
    /// Удаляет группу по её ID.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        string sql = $"DELETE FROM Groups WHERE Id = {id}";
        await _client.ExecuteAsync(sql);
    }
}