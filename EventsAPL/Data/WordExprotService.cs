using EventsAPL.Models;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventsAPL.Data;

public class WordExportService
{
    private readonly TursoClient _client;

    public WordExportService(TursoClient client)
    {
        _client = client;
    }

    public async Task<string> ExportStudentsToWordAsync(string filePath, DateTime? fromDate, DateTime? toDate)
    {
        var data = await LoadDataAsync(fromDate, toDate);
        if (!data.Any()) return "Нет данных для экспорта за указанный период.";

        using var doc = new XWPFDocument();

        // 1. Альбомная ориентация
        CT_SectPr sectPr = new CT_SectPr();
        sectPr.pgSz = new CT_PageSz { orient = ST_PageOrientation.landscape, w = 16838, h = 11906 };
        doc.Document.body.sectPr = sectPr;

        // 2. Заголовок
        var titlePara = doc.CreateParagraph();
        titlePara.Alignment = ParagraphAlignment.CENTER;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText("УЧАСТИЕ СТУДЕНТОВ\nВ МЕРОПРИЯТИЯХ РАЗЛИЧНОГО УРОВНЯ\nЗА " + GetPeriodText(fromDate, toDate));
        titleRun.IsBold = true;
        titleRun.FontSize = 14;
        titleRun.FontFamily = "Times New Roman";
        doc.CreateParagraph();

        // 3. Группировка по Уровням
        var levels = data.GroupBy(x => x.LevelTitle).OrderBy(g => g.Key);
        var footnotes = new List<(string EventTitle, string GroupNumber, string StudentName)>();

        foreach (var levelGroup in levels)
        {
            // Заголовок уровня
            var lvlPara = doc.CreateParagraph();
            var lvlRun = lvlPara.CreateRun();
            lvlRun.SetText(levelGroup.Key.ToUpper());
            lvlRun.IsBold = true;
            lvlRun.FontSize = 12;
            lvlRun.FontFamily = "Times New Roman";

            // Таблица для уровня (6 колонок)
            var table = doc.CreateTable(1, 6);
            SetTableWidths(table, new int[] { 500, 3500, 1500, 2500, 3000, 2500 });

            var headerRow = table.GetRow(0);
            SetCellText(headerRow.GetCell(0), "№ п/п", true);
            SetCellText(headerRow.GetCell(1), "Наименование мероприятия", true);
            SetCellText(headerRow.GetCell(2), "Дата проведения", true);
            SetCellText(headerRow.GetCell(3), "Ф.И.О. руководителя / номинация", true);
            SetCellText(headerRow.GetCell(4), "Ф.И.О. студента / № группы", true);
            SetCellText(headerRow.GetCell(5), "Результаты участия", true);

            int counter = 1;
            var eventsInLevel = levelGroup.GroupBy(x => x.EventId).OrderBy(g => g.First().StartDate);

            foreach (var evGroup in eventsInLevel)
            {
                var first = evGroup.First();
                var students = evGroup.Where(x => x.StudentId.HasValue).ToList();
                if (!students.Any()) continue;

                var row = table.CreateRow();
                SetCellText(row.GetCell(0), counter.ToString(), false);
                SetCellText(row.GetCell(1), first.EventTitle, false);
                SetCellText(row.GetCell(2), FormatDate(first.StartDate, first.EndDate), false);

                string leader = string.Join("\n", evGroup.Select(x => x.LeaderName).Where(x => !string.IsNullOrEmpty(x)).Distinct());
                SetCellText(row.GetCell(3), leader, false);

                // Логика сносок (>10 студентов из одной группы)
                var studentsByGroup = students.GroupBy(s => s.GroupNumber);
                var studentNames = new List<string>();
                var results = new List<string>();

                foreach (var sGroup in studentsByGroup)
                {
                    if (sGroup.Count() > 10)
                    {
                        studentNames.Add($"{sGroup.Key} - {sGroup.Count()} чел.[^]");
                        results.Add(string.Join("; ", sGroup.Select(x => x.ResultText).Distinct()));
                        foreach (var s in sGroup) footnotes.Add((first.EventTitle, s.GroupNumber, s.StudentName));
                    }
                    else
                    {
                        studentNames.Add(string.Join(", ", sGroup.Select(x => $"{x.StudentName} ({x.GroupNumber})")));
                        results.Add(string.Join("; ", sGroup.Select(x => x.ResultText).Distinct()));
                    }
                }

                SetCellText(row.GetCell(4), string.Join("\n", studentNames), false);
                SetCellText(row.GetCell(5), string.Join("\n", results.Where(r => !string.IsNullOrEmpty(r))), false);
                counter++;
            }
            doc.CreateParagraph();
        }

        // 4. Добавляем сноски в конец
        if (footnotes.Any())
        {
            var fnPara = doc.CreateParagraph();
            var fnRun = fnPara.CreateRun();
            fnRun.SetText("[^] Расшифровка:");
            fnRun.IsBold = true;

            foreach (var group in footnotes.GroupBy(f => f.EventTitle))
            {
                var evPara = doc.CreateParagraph();
                evPara.CreateRun().SetText($"{group.Key}:");

                foreach (var fn in group)
                {
                    var sp = doc.CreateParagraph();
                    sp.CreateRun().SetText($"  - {fn.GroupNumber}: {fn.StudentName}");
                }
            }
        }

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        doc.Write(fs);
        return "Отчет по студентам успешно сформирован!";
    }

    private async Task<List<ExportRow>> LoadDataAsync(DateTime? from, DateTime? to)
    {
        string dateFilter = "";
        if (from.HasValue) dateFilter += $" AND e.StartDate >= '{from.Value:yyyy-MM-dd}'";
        if (to.HasValue) dateFilter += $" AND e.StartDate <= '{to.Value:yyyy-MM-dd}'";

        // Используем COALESCE, чтобы избежать NULL в строках при склейке
        string sql = $@"
        SELECT 
            e.Id, e.Title, e.StartDate, e.EndDate,
            el.Title AS LevelTitle,
            ep.StudentId, s.LastName, s.FirstName, s.MiddleName, g.Number AS GroupNumber,
            rt.Title AS ResultType, rl.Title AS ResultLevel,
            (SELECT GROUP_CONCAT(COALESCE(t.LastName, '') || ' ' || COALESCE(t.FirstName, '') || ' ' || COALESCE(t.MiddleName, ''), ', ') 
             FROM EventLeaders elr 
             JOIN Teachers t ON elr.TeacherId = t.Id 
             WHERE elr.EventId = e.Id) AS Leaders
        FROM Events e
        LEFT JOIN EventLevels el ON e.EventLevelId = el.Id
        LEFT JOIN EventParticipants ep ON e.Id = ep.EventId
        LEFT JOIN Students s ON ep.StudentId = s.Id
        LEFT JOIN Groups g ON s.GroupId = g.Id
        LEFT JOIN ResultTypes rt ON ep.ResultTypeId = rt.Id
        LEFT JOIN ResultLevels rl ON ep.ResultLevelId = rl.Id
        WHERE ep.StudentId IS NOT NULL {dateFilter}
        ORDER BY el.Title, e.StartDate";

        var json = await _client.ExecuteAsync(sql);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 1. Проверяем, не вернул ли прокси/сервер общую ошибку
        if (root.TryGetProperty("error", out var errorElement))
        {
            throw new Exception($"Ошибка от сервера: {errorElement.GetString()}");
        }

        // 2. Проверяем наличие ключа "results"
        if (!root.TryGetProperty("results", out var results))
        {
            throw new Exception("Сервер вернул неожиданный формат ответа (нет ключа 'results').");
        }

        var firstResult = results[0];

        // 3. Проверяем, не вернул ли Turso ошибку SQL
        if (firstResult.TryGetProperty("type", out var type) && type.GetString() == "error")
        {
            var errObj = firstResult.GetProperty("error");
            throw new Exception($"Ошибка SQL: {errObj.GetProperty("message").GetString()}");
        }

        // 4. Достаем строки
        if (!firstResult.TryGetProperty("response", out var response)) return new List<ExportRow>();
        if (!response.TryGetProperty("result", out var result)) return new List<ExportRow>();
        if (!result.TryGetProperty("rows", out var rows)) return new List<ExportRow>();

        var list = new List<ExportRow>();

        foreach (var row in rows.EnumerateArray())
        {
            var r = new ExportRow();

            // Локальные функции для безопасного чтения
            string SafeStr(JsonElement el)
            {
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null)
                    return v.GetString() ?? "";
                return "";
            }

            int SafeInt(JsonElement el) => int.TryParse(SafeStr(el), out int i) ? i : 0;
            DateTime SafeDate(JsonElement el) => DateTime.TryParse(SafeStr(el), out DateTime d) ? d : DateTime.MinValue;

            r.EventId = SafeInt(row[0]);
            r.EventTitle = SafeStr(row[1]);
            r.StartDate = SafeDate(row[2]);

            // Безопасное чтение даты окончания
            if (row[3].ValueKind == JsonValueKind.Object && row[3].TryGetProperty("value", out var ed) && ed.ValueKind != JsonValueKind.Null)
            {
                r.EndDate = DateTime.TryParse(ed.GetString(), out DateTime d) ? d : null;
            }

            r.LevelTitle = SafeStr(row[4]);
            r.StudentId = SafeInt(row[5]);

            string ln = SafeStr(row[6]);
            string fn = SafeStr(row[7]);
            string mn = SafeStr(row[8]);
            r.StudentName = $"{ln} {fn} {mn}".Trim();

            r.GroupNumber = SafeStr(row[9]);
            string rt = SafeStr(row[10]);
            string rl = SafeStr(row[11]);
            r.ResultText = $"{rt} {rl}".Trim();

            r.LeaderName = SafeStr(row[12]);

            list.Add(r);
        }
        return list;
    }

    private void SetCellText(XWPFTableCell cell, string text, bool isBold)
    {
        while (cell.Paragraphs.Count > 0) cell.RemoveParagraph(0);
        var p = cell.AddParagraph();
        p.Alignment = ParagraphAlignment.LEFT;
        var r = p.CreateRun();
        r.SetText(text);
        r.IsBold = isBold;
        r.FontFamily = "Times New Roman";
        r.FontSize = 10;
    }

    private void SetTableWidths(XWPFTable table, int[] widths)
    {
        for (int i = 0; i < widths.Length; i++) table.SetColumnWidth(i, (ulong)widths[i]);
    }

    private string FormatDate(DateTime start, DateTime? end)
    {
        if (end.HasValue && end.Value.Date != start.Date)
            return $"{start:dd.MM.yyyy}-{end.Value:dd.MM.yyyy}";
        return start.ToString("dd.MM.yyyy");
    }

    private string GetPeriodText(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue) return $"{from.Value:yyyy}-{to.Value:yyyy} учебный год";
        if (from.HasValue) return $"с {from.Value:dd.MM.yyyy}";
        if (to.HasValue) return $"по {to.Value:dd.MM.yyyy}";
        return "весь период";
    }
}