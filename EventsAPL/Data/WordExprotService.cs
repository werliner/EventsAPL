using EventsAPL.Models;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;
using System.IO;
using System.Text.Json;

namespace EventsAPL.Data;

public class WordExportService
{
    private readonly TursoClient _client;

    public WordExportService(TursoClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Экспорт отчёта по студентам.
    /// Создаёт два файла:
    ///   1. Основной отчёт (filePath)
    ///   2. Приложение с расшифровкой больших групп (рядом, с префиксом "Приложение_к_отчету_")
    /// Возвращает путь к основному файлу.
    /// </summary>
    public async Task<string> ExportStudentsToWordAsync(string filePath, DateTime? fromDate, DateTime? toDate)
    {
        var data = await LoadDataAsync(fromDate, toDate);
        if (!data.Any()) return "Нет данных для экспорта за указанный период.";

        // ========== 1. ОСНОВНОЙ ОТЧЁТ ==========
        using var doc = new XWPFDocument();

        // Альбомная ориентация
        CT_SectPr sectPr = new CT_SectPr();
        sectPr.pgSz = new CT_PageSz { orient = ST_PageOrientation.landscape, w = 16838, h = 11906 };
        doc.Document.body.sectPr = sectPr;

        // Заголовок
        var titlePara = doc.CreateParagraph();
        titlePara.Alignment = ParagraphAlignment.CENTER;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText("УЧАСТИЕ СТУДЕНТОВ\nВ МЕРОПРИЯТИЯХ РАЗЛИЧНОГО УРОВНЯ\nЗА " + GetPeriodText(fromDate, toDate));
        titleRun.IsBold = true;
        titleRun.FontSize = 14;
        titleRun.FontFamily = "Times New Roman";
        doc.CreateParagraph();

        // Группировка по уровням
        var levels = data.GroupBy(x => x.LevelTitle).OrderBy(g => g.Key);

        // Сюда собираем данные для приложения:
        // Key = название мероприятия, Value = список (группа, результат, список ФИО)
        var appendixData = new List<(string EventTitle, string GroupNumber, string ResultText, List<string> StudentNames)>();

        // Для общей статистики
        var globalStats = new Dictionary<string, (int events, int results, int partDup, int partUniq)>();

        foreach (var levelGroup in levels)
        {
            // Заголовок уровня
            var lvlPara = doc.CreateParagraph();
            lvlPara.Alignment = ParagraphAlignment.CENTER;
            var lvlRun = lvlPara.CreateRun();
            lvlRun.SetText(levelGroup.Key.ToUpper());
            lvlRun.IsBold = true;
            lvlRun.FontSize = 12;
            lvlRun.FontFamily = "Times New Roman";

            // Таблица для уровня
            var table = doc.CreateTable(1, 6);
            SetTableWidths(table, new int[] { 500, 4000, 1500, 3000, 3000, 2500 });

            var headerRow = table.GetRow(0);
            SetCellText(headerRow.GetCell(0), "№\nп/п", true);
            SetCellText(headerRow.GetCell(1), "Наименование мероприятия", true);
            SetCellText(headerRow.GetCell(2), "Дата проведения\nмероприятия", true);
            SetCellText(headerRow.GetCell(3), "Ф.И.О.\nруководителя\n/номинация или\nтема работы", true);
            SetCellText(headerRow.GetCell(4), "Ф.И.О. студента\n\n№ группы", true);
            SetCellText(headerRow.GetCell(5), "Результаты участия\nв мероприятии", true);

            int counter = 1;
            var eventsInLevel = levelGroup.GroupBy(x => x.EventId).OrderBy(g => g.First().StartDate).ToList();

            int levelPartDup = 0;
            var uniqueStudentsInLevel = new HashSet<int>();
            int levelResults = 0;

            foreach (var evGroup in eventsInLevel)
            {
                var first = evGroup.First();
                var students = evGroup.Where(x => x.StudentId.HasValue).ToList();
                if (!students.Any()) continue;

                int startRowIndex = table.NumberOfRows;

                // Группируем по Группе и Результату
                var studentsByGroupAndResult = students.GroupBy(s => new { s.GroupNumber, s.ResultText });
                var studentNames = new List<string>();
                var results = new List<string>();

                foreach (var sGroup in studentsByGroupAndResult)
                {
                    if (sGroup.Count() > 10)
                    {
                        // ===== БОЛЬШЕ 10 ЧЕЛОВЕК → ссылка на приложение =====
                        studentNames.Add($"{sGroup.Key.GroupNumber} - {sGroup.Count()} чел. [Приложение]");
                        results.Add(sGroup.Key.ResultText);

                        // Собираем данные для приложения
                        appendixData.Add((
                            first.EventTitle,
                            sGroup.Key.GroupNumber,
                            sGroup.Key.ResultText,
                            sGroup.Select(s => s.StudentName).ToList()
                        ));
                    }
                    else
                    {
                        // <= 10 человек → пишем прямо в таблицу
                        foreach (var s in sGroup)
                        {
                            studentNames.Add($"{s.StudentName} ({s.GroupNumber})");
                            results.Add(s.ResultText);
                        }
                    }

                    // Статистика
                    levelPartDup += sGroup.Count();
                    foreach (var s in sGroup)
                        if (s.StudentId.HasValue) uniqueStudentsInLevel.Add(s.StudentId.Value);
                    levelResults += sGroup.Count();
                }

                // Создаём строки таблицы
                for (int i = 0; i < studentNames.Count; i++)
                {
                    var row = table.CreateRow();

                    if (i == 0)
                    {
                        SetCellText(row.GetCell(0), counter.ToString(), false);
                        SetCellText(row.GetCell(1), first.EventTitle, false);
                        SetCellText(row.GetCell(2), FormatDate(first.StartDate, first.EndDate), false);

                        string leader = string.Join("\n",
                            evGroup.Select(x => x.LeaderName).Where(x => !string.IsNullOrEmpty(x)).Distinct());
                        SetCellText(row.GetCell(3), leader, false);
                    }
                    else
                    {
                        SetCellText(row.GetCell(0), "", false);
                        SetCellText(row.GetCell(1), "", false);
                        SetCellText(row.GetCell(2), "", false);
                        SetCellText(row.GetCell(3), "", false);
                    }

                    SetCellText(row.GetCell(4), studentNames[i], false);
                    SetCellText(row.GetCell(5), results[i], false);
                }

                // Вертикальное объединение ячеек
                if (studentNames.Count > 1)
                {
                    for (int col = 0; col <= 3; col++)
                    {
                        var firstCell = table.GetRow(startRowIndex).GetCell(col);
                        var tcPr1 = firstCell.GetCTTc().tcPr ?? firstCell.GetCTTc().AddNewTcPr();
                        var vMerge1 = tcPr1.vMerge ?? tcPr1.AddNewVMerge();
                        vMerge1.val = ST_Merge.restart;

                        for (int r = 1; r < studentNames.Count; r++)
                        {
                            var nextCell = table.GetRow(startRowIndex + r).GetCell(col);
                            var tcPr2 = nextCell.GetCTTc().tcPr ?? nextCell.GetCTTc().AddNewTcPr();
                            var vMerge2 = tcPr2.vMerge ?? tcPr2.AddNewVMerge();
                            vMerge2.val = ST_Merge.@continue;
                        }
                    }
                }

                counter++;
            }

            // Статистика по уровню
            int levelPartUniq = uniqueStudentsInLevel.Count;
            globalStats[levelGroup.Key] = (eventsInLevel.Count, levelResults, levelPartDup, levelPartUniq);

            AddSummaryRow(doc, "Всего мероприятий:", eventsInLevel.Count.ToString());
            AddSummaryRow(doc, "Всего результатов участия (победители, призеры, участники):", levelResults.ToString());
            AddSummaryRow(doc, "Всего участников (с повторением в разных мероприятиях уровня/без повторений на уровне):",
                $"{levelPartDup}/{levelPartUniq}");

            doc.CreateParagraph();
        }

        // ОБЩАЯ СВОДНАЯ СТАТИСТИКА
        var pageBreakPara = doc.CreateParagraph();
        pageBreakPara.CreateRun().AddBreak(BreakType.PAGE);

        var overallTitle = doc.CreateParagraph();
        overallTitle.Alignment = ParagraphAlignment.CENTER;
        var overallRun = overallTitle.CreateRun();
        overallRun.SetText("СВОДНАЯ СТАТИСТИКА ЗА " + GetPeriodText(fromDate, toDate).ToUpper());
        overallRun.IsBold = true;
        overallRun.FontSize = 14;
        overallRun.FontFamily = "Times New Roman";

        int totalLevels = globalStats.Count;
        var statsTable = doc.CreateTable(totalLevels + 2, 2);
        SetTableWidths(statsTable, new int[] { 8000, 4000 });

        int rIdx = 0;
        foreach (var kvp in globalStats)
        {
            var row = statsTable.GetRow(rIdx++);
            SetCellText(row.GetCell(0), $"Всего по уровню: {kvp.Key}", true);
            SetCellText(row.GetCell(1),
                $"{kvp.Value.events} меропр. / {kvp.Value.results} рез. / {kvp.Value.partDup} уч. (уник. {kvp.Value.partUniq})",
                false);
        }

        var totalRow = statsTable.GetRow(rIdx);
        SetCellText(totalRow.GetCell(0), "ИТОГО ЗА ВЕСЬ ПЕРИОД", true);
        SetCellText(totalRow.GetCell(1), "", false);

        // Сохраняем основной отчёт
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            doc.Write(fs);
        }

        // ========== 2. ПРИЛОЖЕНИЕ (если есть данные) ==========
        if (appendixData.Any())
        {
            string appendixPath = BuildAppendixPath(filePath);
            GenerateAppendixDocument(appendixPath, appendixData, fromDate, toDate);
        }

        return "Отчёт по студентам успешно сформирован!" +
               (appendixData.Any() ? "\nПриложение сохранено рядом с основным файлом." : "");
    }

    // ==================== ГЕНЕРАЦИЯ ПРИЛОЖЕНИЯ ====================

    private void GenerateAppendixDocument(
    string appendixPath,
    List<(string EventTitle, string GroupNumber, string ResultText, List<string> StudentNames)> appendixData,
    DateTime? fromDate, DateTime? toDate)
    {
        using var doc = new XWPFDocument();

        // Портретная ориентация
        CT_SectPr sectPr = new CT_SectPr();
        sectPr.pgSz = new CT_PageSz { orient = ST_PageOrientation.portrait, w = 11906, h = 16838 };
        doc.Document.body.sectPr = sectPr;

        // Заголовок
        var titlePara = doc.CreateParagraph();
        titlePara.Alignment = ParagraphAlignment.CENTER;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText("ПРИЛОЖЕНИЕ\nк отчёту об участии студентов\nв мероприятиях различного уровня\nза " + GetPeriodText(fromDate, toDate));
        titleRun.IsBold = true;
        titleRun.FontSize = 14;
        titleRun.FontFamily = "Times New Roman";
        doc.CreateParagraph(); // пустая строка после заголовка

        // Создаём таблицу: заголовок + строки данных
        int totalRows = 1 + appendixData.Count; // 1 = шапка
        var table = doc.CreateTable(totalRows, 4);
        SetTableWidths(table, new int[] { 600, 4500, 2000, 4806 });

        // Шапка таблицы
        var headerRow = table.GetRow(0);
        SetCellText(headerRow.GetCell(0), "№", true);
        SetCellText(headerRow.GetCell(1), "Наименование мероприятия", true);
        SetCellText(headerRow.GetCell(2), "Группа\n(результат)", true);
        SetCellText(headerRow.GetCell(3), "Ф.И.О. студентов", true);

        // Строки данных
        int counter = 1;
        foreach (var entry in appendixData)
        {
            var row = table.GetRow(counter);

            SetCellText(row.GetCell(0), counter.ToString(), false);
            SetCellText(row.GetCell(1), entry.EventTitle, false);
            SetCellText(row.GetCell(2), $"{entry.GroupNumber}\n({entry.ResultText})", false);

            // ФИО через запятую, перенос по словам
            string names = string.Join(", ", entry.StudentNames);
            SetCellText(row.GetCell(3), names, false);

            counter++;
        }

        // Применяем границы ко всем ячейкам таблицы
        ApplyTableBorders(table);

        using var fs = new FileStream(appendixPath, FileMode.Create, FileAccess.Write);
        doc.Write(fs);
    }

    /// <summary>
    /// Применяет тонкие чёрные границы ко всем ячейкам таблицы.
    /// </summary>
    private void ApplyTableBorders(XWPFTable table)
    {
        var tbl = table.GetCTTbl();
        var tblPr = tbl.tblPr ?? tbl.AddNewTblPr();

        var borders = tblPr.tblBorders ?? tblPr.AddNewTblBorders();

        // top, bottom, left, right, insideH, insideV
        var top = borders.top ?? borders.AddNewTop();
        top.val = ST_Border.single;
        top.sz = 4;
        top.color = "000000";

        var bottom = borders.bottom ?? borders.AddNewBottom();
        bottom.val = ST_Border.single;
        bottom.sz = 4;
        bottom.color = "000000";

        var left = borders.left ?? borders.AddNewLeft();
        left.val = ST_Border.single;
        left.sz = 4;
        left.color = "000000";

        var right = borders.right ?? borders.AddNewRight();
        right.val = ST_Border.single;
        right.sz = 4;
        right.color = "000000";

        var insideH = borders.insideH ?? borders.AddNewInsideH();
        insideH.val = ST_Border.single;
        insideH.sz = 4;
        insideH.color = "000000";

        var insideV = borders.insideV ?? borders.AddNewInsideV();
        insideV.val = ST_Border.single;
        insideV.sz = 4;
        insideV.color = "000000";
    }

    /// <summary>
    /// Строит путь к файлу приложения рядом с основным файлом.
    /// Например: C:\Reports\Отчет.docx → C:\Reports\Приложение_к_отчету_Студенты_20261120.docx
    /// </summary>
    private string BuildAppendixPath(string mainFilePath)
    {
        string dir = Path.GetDirectoryName(mainFilePath) ?? "";
        string dateStr = DateTime.Now.ToString("yyyyMMdd");
        return Path.Combine(dir, $"Приложение_к_отчету_Студенты_{dateStr}.docx");
    }

    // ==================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================

    private void AddSummaryRow(XWPFDocument doc, string label, string value)
    {
        var para = doc.CreateParagraph();
        para.Alignment = ParagraphAlignment.RIGHT;
        var run = para.CreateRun();
        run.SetText($"{label} {value}");
        run.IsBold = true;
        run.FontFamily = "Times New Roman";
        run.FontSize = 11;
    }

    private void SetCellText(XWPFTableCell cell, string text, bool isBold)
    {
        while (cell.Paragraphs.Count > 0) cell.RemoveParagraph(0);

        var p = cell.AddParagraph();
        p.Alignment = ParagraphAlignment.LEFT;

        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            var r = p.CreateRun();
            r.SetText(lines[i]);
            r.IsBold = isBold;
            r.FontFamily = "Times New Roman";
            r.FontSize = 10;

            if (i < lines.Length - 1)
                r.AddBreak(BreakType.TEXTWRAPPING);
        }
    }

    private void SetTableWidths(XWPFTable table, int[] widths)
    {
        for (int i = 0; i < widths.Length; i++)
            table.SetColumnWidth(i, (ulong)widths[i]);
    }

    private string FormatDate(DateTime start, DateTime? end)
    {
        if (end.HasValue && end.Value.Date != start.Date)
            return $"{start:dd.MM.yyyy}-{end.Value:dd.MM.yyyy}";
        return start.ToString("dd.MM.yyyy");
    }

    private string GetPeriodText(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue) return $"{from.Value:yyyy}-{to.Value:yyyy} учебного года";
        if (from.HasValue) return $"с {from.Value:dd.MM.yyyy}";
        if (to.HasValue) return $"по {to.Value:dd.MM.yyyy}";
        return "весь период";
    }

    private async Task<List<ExportRow>> LoadDataAsync(DateTime? from, DateTime? to)
    {
        string dateFilter = "";
        if (from.HasValue) dateFilter += $" AND e.StartDate >= '{from.Value:yyyy-MM-dd}'";
        if (to.HasValue) dateFilter += $" AND e.StartDate <= '{to.Value:yyyy-MM-dd}'";

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

        if (doc.RootElement.TryGetProperty("error", out var err))
            throw new Exception($"Ошибка сервера: {err.GetString()}");
        if (!doc.RootElement.TryGetProperty("results", out var results))
            return new List<ExportRow>();

        var firstResult = results[0];
        if (firstResult.TryGetProperty("type", out var type) && type.GetString() == "error")
            throw new Exception($"Ошибка SQL: {firstResult.GetProperty("error").GetProperty("message").GetString()}");

        if (!firstResult.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("rows", out var rows))
            return new List<ExportRow>();

        var list = new List<ExportRow>();
        foreach (var row in rows.EnumerateArray())
        {
            var r = new ExportRow();
            string SafeStr(JsonElement el) =>
                el.ValueKind == JsonValueKind.Object && el.TryGetProperty("value", out var v) &&
                v.ValueKind != JsonValueKind.Null ? v.GetString() ?? "" : "";
            int SafeInt(JsonElement el) => int.TryParse(SafeStr(el), out int i) ? i : 0;
            DateTime SafeDate(JsonElement el) => DateTime.TryParse(SafeStr(el), out DateTime d) ? d : DateTime.MinValue;

            r.EventId = SafeInt(row[0]);
            r.EventTitle = SafeStr(row[1]);
            r.StartDate = SafeDate(row[2]);
            if (row[3].ValueKind == JsonValueKind.Object && row[3].TryGetProperty("value", out var ed) &&
                ed.ValueKind != JsonValueKind.Null)
                r.EndDate = DateTime.TryParse(ed.GetString(), out DateTime d) ? d : null;

            r.LevelTitle = SafeStr(row[4]);
            r.StudentId = SafeInt(row[5]);

            string ln = SafeStr(row[6]), fn = SafeStr(row[7]), mn = SafeStr(row[8]);
            r.StudentName = $"{ln} {fn} {mn}".Trim();

            r.GroupNumber = SafeStr(row[9]);
            r.ResultText = $"{SafeStr(row[10])} {SafeStr(row[11])}".Trim();
            r.LeaderName = SafeStr(row[12]);

            list.Add(r);
        }
        return list;
    }
}