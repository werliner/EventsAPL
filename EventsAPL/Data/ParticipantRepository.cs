using System.Text.Json;
using EventsAPL.Models;

namespace EventsAPL.Data;

public class ParticipantRepository
{
    private readonly TursoClient _client;

    public ParticipantRepository(TursoClient client)
    {
        _client = client;
    }

    public async Task<List<EventParticipant>> GetByEventAsync(int eventId)
    {
        string sql = @"
            SELECT 
                ep.Id, ep.EventId, ep.StudentId, ep.TeacherId, ep.ResultTypeId, ep.ResultLevelId,
                s.LastName AS StudentLastName, s.FirstName AS StudentFirstName, s.MiddleName AS StudentMiddleName,
                t.LastName AS TeacherLastName, t.FirstName AS TeacherFirstName, t.MiddleName AS TeacherMiddleName,
                rt.Title AS ResultTypeTitle, rl.Title AS ResultLevelTitle
            FROM EventParticipants ep
            LEFT JOIN Students s ON ep.StudentId = s.Id
            LEFT JOIN Teachers t ON ep.TeacherId = t.Id
            LEFT JOIN ResultTypes rt ON ep.ResultTypeId = rt.Id
            LEFT JOIN ResultLevels rl ON ep.ResultLevelId = rl.Id
            WHERE ep.EventId = " + eventId;

        var jsonResponse = await _client.ExecuteAsync(sql);
        return ParseParticipants(jsonResponse);
    }

    public async Task AddAsync(EventParticipant participant)
    {
        string studentIdStr = participant.StudentId.HasValue ? participant.StudentId.Value.ToString() : "NULL";
        string teacherIdStr = participant.TeacherId.HasValue ? participant.TeacherId.Value.ToString() : "NULL";
        string typeIdStr = participant.ResultTypeId.HasValue ? participant.ResultTypeId.Value.ToString() : "NULL";
        string levelIdStr = participant.ResultLevelId.HasValue ? participant.ResultLevelId.Value.ToString() : "NULL";

        string sql = $"INSERT INTO EventParticipants (EventId, StudentId, TeacherId, ResultTypeId, ResultLevelId) VALUES ({participant.EventId}, {studentIdStr}, {teacherIdStr}, {typeIdStr}, {levelIdStr})";
        await _client.ExecuteAsync(sql);
    }

    public async Task DeleteAllByEventAsync(int eventId)
    {
        await _client.ExecuteAsync($"DELETE FROM EventParticipants WHERE EventId = {eventId}");
    }

    private List<EventParticipant> ParseParticipants(string jsonResponse)
    {
        var participants = new List<EventParticipant>();
        using var document = JsonDocument.Parse(jsonResponse);

        if (!document.RootElement.TryGetProperty("results", out var results)) return participants;

        var rows = results[0].GetProperty("response").GetProperty("result").GetProperty("rows");

        foreach (var row in rows.EnumerateArray())
        {
            var p = new EventParticipant
            {
                Id = int.Parse(row[0].GetProperty("value").GetString()!),
                EventId = int.Parse(row[1].GetProperty("value").GetString()!),
            };

            if (row[2].TryGetProperty("value", out var sId) && sId.ValueKind != JsonValueKind.Null) p.StudentId = int.Parse(sId.GetString()!);
            if (row[3].TryGetProperty("value", out var tId) && tId.ValueKind != JsonValueKind.Null) p.TeacherId = int.Parse(tId.GetString()!);
            if (row[4].TryGetProperty("value", out var rtId) && rtId.ValueKind != JsonValueKind.Null) p.ResultTypeId = int.Parse(rtId.GetString()!);
            if (row[5].TryGetProperty("value", out var rlId) && rlId.ValueKind != JsonValueKind.Null) p.ResultLevelId = int.Parse(rlId.GetString()!);

            if (p.StudentId.HasValue)
            {
                string ln = row[6].GetProperty("value").GetString() ?? "";
                string fn = row[7].GetProperty("value").GetString() ?? "";
                string mn = row[8].GetProperty("value").GetString() ?? "";
                p.ParticipantName = $"{ln} {fn} {mn}".Trim();
            }
            else if (p.TeacherId.HasValue)
            {
                string ln = row[9].GetProperty("value").GetString() ?? "";
                string fn = row[10].GetProperty("value").GetString() ?? "";
                string mn = row[11].GetProperty("value").GetString() ?? "";
                p.ParticipantName = $"{ln} {fn} {mn}".Trim();
            }

            string rt = row[12].TryGetProperty("value", out var rtVal) && rtVal.ValueKind != JsonValueKind.Null ? rtVal.GetString()! : "";
            string rl = row[13].TryGetProperty("value", out var rlVal) && rlVal.ValueKind != JsonValueKind.Null ? rlVal.GetString()! : "";
            p.ResultDisplay = $"{rt} {rl}".Trim();

            participants.Add(p);
        }
        return participants;
    }
}