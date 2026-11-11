using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NPOI.XWPF.UserModel;
using EventsAPL.Models;

namespace EventsAPL.Data;

public class StudentListImporter
{
    private readonly TursoClient _client;
    private readonly GroupRepository _groupRepo;
    private readonly StudentRepository _studentRepo;

    public StudentListImporter(TursoClient client, GroupRepository groupRepo, StudentRepository studentRepo)
    {
        _client = client;
        _groupRepo = groupRepo;
        _studentRepo = studentRepo;
    }

    public async Task<string> ImportFromDocxAsync(string filePath)
    {
        var report = new StringBuilder();
        var fileName = Path.GetFileName(filePath);
        report.AppendLine($"📄 {fileName}");

        try
        {
            using var stream = File.OpenRead(filePath);
            using var doc = new XWPFDocument(stream);

            var (groupNumber, specialty) = ExtractGroupInfo(doc);

            if (string.IsNullOrEmpty(groupNumber))
            {
                report.AppendLine("  ❌ Не удалось найти номер группы в документе");
                return report.ToString();
            }

            report.AppendLine($"  Группа: {groupNumber}");
            if (!string.IsNullOrEmpty(specialty))
                report.AppendLine($"  Специальность: {specialty}");

            var groupId = await EnsureGroupExistsAsync(groupNumber, specialty);

            var table = doc.Tables.FirstOrDefault();
            if (table == null)
            {
                report.AppendLine("  ❌ В документе нет таблицы со студентами");
                return report.ToString();
            }

            var existingStudents = await _studentRepo.GetByGroupAsync(groupId);
            var valuesToInsert = new List<string>();
            int skipped = 0, invalid = 0;

            foreach (var row in table.Rows)
            {
                var cells = row.GetTableCells();
                if (cells.Count < 2) continue;

                string fio = cells[1].GetText()?.Trim() ?? "";
                if (string.IsNullOrEmpty(fio)) continue;

                // Очистка от примечаний
                fio = Regex.Replace(fio, @"\s*\([^)]*\)", "");
                fio = Regex.Replace(fio, @"\s+[А-Я]$", "");
                fio = Regex.Replace(fio, @"\s+", " ").Trim();

                if (fio.Contains("Фамилия", StringComparison.OrdinalIgnoreCase)) continue;

                var (lastName, firstName, middleName) = ParseFio(fio);

                if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
                {
                    invalid++;
                    continue;
                }

                bool exists = existingStudents.Any(s =>
                    string.Equals(s.LastName, lastName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.FirstName, firstName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.MiddleName, middleName, StringComparison.OrdinalIgnoreCase));

                if (exists) { skipped++; continue; }

                // Собираем в список для BULK INSERT
                valuesToInsert.Add($"({groupId}, '{Escape(lastName)}', '{Escape(firstName)}', '{Escape(middleName)}')");
            }

            int added = valuesToInsert.Count;

            // ОДИН запрос для всех новых студентов
            if (added > 0)
            {
                string sql = $"INSERT INTO Students (GroupId, LastName, FirstName, MiddleName) VALUES {string.Join(", ", valuesToInsert)}";
                await _client.ExecuteAsync(sql);
            }

            report.AppendLine($"  ✅ Добавлено: {added}  |  Пропущено дублей: {skipped}" +
                              (invalid > 0 ? $"  |  Некорректных: {invalid}" : ""));
        }
        catch (Exception ex)
        {
            report.AppendLine($"  ❌ Ошибка: {ex.Message}");
        }

        return report.ToString();
    }

    private (string groupNumber, string specialty) ExtractGroupInfo(XWPFDocument doc)
    {
        string groupNumber = "";
        string specialty = "";
        int nonEmptyCount = 0;

        foreach (var para in doc.Paragraphs)
        {
            string text = para.ParagraphText?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) continue;

            nonEmptyCount++;

            if (string.IsNullOrEmpty(groupNumber))
            {
                var match = Regex.Match(text, @"[Гг]руппа\s*:?\s*([А-Яа-яA-Za-z0-9\-]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    groupNumber = match.Groups[1].Value.Trim();
                    continue;
                }
            }

            if (nonEmptyCount <= 3 && string.IsNullOrEmpty(groupNumber))
            {
                var codeMatch = Regex.Match(text, @"\b([А-Я]{1,4}-\d{2}[А-Я]?)\b", RegexOptions.IgnoreCase);
                if (codeMatch.Success)
                {
                    groupNumber = codeMatch.Groups[1].Value.Trim();
                    continue;
                }
            }

            if (nonEmptyCount == 2 && string.IsNullOrEmpty(specialty)
                && !Regex.IsMatch(text, @"^(Приказ|Пр\.|№)", RegexOptions.IgnoreCase))
            {
                specialty = Regex.Replace(text, @"^(Специальность\s*)", "", RegexOptions.IgnoreCase).Trim();
                continue;
            }

            if (!string.IsNullOrEmpty(groupNumber) && !string.IsNullOrEmpty(specialty)) break;
            if (nonEmptyCount > 4) break;
        }

        return (groupNumber, specialty);
    }

    private async Task<int> EnsureGroupExistsAsync(string groupNumber, string specialty)
    {
        var groups = await _groupRepo.GetAllAsync();
        var group = groups.FirstOrDefault(g =>
            string.Equals(g.Number, groupNumber, StringComparison.OrdinalIgnoreCase));

        if (group != null)
        {
            if (string.IsNullOrEmpty(group.Title) && !string.IsNullOrEmpty(specialty))
            {
                await _client.ExecuteAsync(
                    $"UPDATE Groups SET Title = '{Escape(specialty)}' WHERE Id = {group.Id}");
            }
            return group.Id;
        }

        await _client.ExecuteAsync(
            $"INSERT INTO Groups (Number, Title) VALUES ('{Escape(groupNumber)}', '{Escape(specialty ?? "")}')");

        groups = await _groupRepo.GetAllAsync();
        return groups.First(g => string.Equals(g.Number, groupNumber, StringComparison.OrdinalIgnoreCase)).Id;
    }

    private (string lastName, string firstName, string middleName) ParseFio(string fio)
    {
        var parts = fio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("", "", ""),
            1 => (parts[0], "", ""),
            2 => (parts[0], parts[1], ""),
            _ => (parts[0], parts[1], string.Join(" ", parts.Skip(2)))
        };
    }

    private string Escape(string text) => (text ?? "").Replace("'", "''");
}