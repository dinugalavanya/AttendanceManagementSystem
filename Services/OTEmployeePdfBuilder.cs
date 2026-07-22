using System.Globalization;
using System.Text;
using AttendanceManagementSystem.ViewModels;

namespace AttendanceManagementSystem.Services
{
    public static class OTEmployeePdfBuilder
    {
        public static byte[] Build(List<OTEmployeeViewModel> employees, string filterLabel, OTSummaryViewModel summary, DateTime generatedAt)
        {
            var pageStreams = BuildPageStreams(employees, filterLabel, summary, generatedAt);
            return BuildMultiPagePdf(pageStreams);
        }

        private static List<string> BuildPageStreams(List<OTEmployeeViewModel> employees, string filterLabel, OTSummaryViewModel summary, DateTime generatedAt)
        {
            var pageStreams = new List<string>();
            var sb = new StringBuilder();

            var left = 30m;
            var right = 565m;
            var tableWidth = right - left;

            var dark = (r: 0.067m, g: 0.094m, b: 0.153m);
            var soft = (r: 0.216m, g: 0.255m, b: 0.318m);
            var hdrTxt = (r: 0.118m, g: 0.227m, b: 0.541m);
            var hdrBg = (r: 0.933m, g: 0.957m, b: 1.0m);
            var border = (r: 0.796m, g: 0.835m, b: 0.882m);
            var purple = (r: 0.486m, g: 0.227m, b: 0.929m);
            var totBg = (r: 0.972m, g: 0.980m, b: 1.0m);

            var c1 = left + 115m; // Department start
            var c2 = c1 + 85m;    // OT Hours start
            var c3 = c2 + 65m;    // Status start
            var c4 = c3 + 80m;    // Task Description start

            int pageNumber = 1;
            decimal iy = 810m;

            void StartNewPageHeader(bool isFirstPage)
            {
                sb.Clear();
                if (isFirstPage)
                {
                    AppendText(sb, "F2", 22m, left, 810, "PulseHR / OT Management System", dark.r, dark.g, dark.b);
                    AppendText(sb, "F2", 15m, left, 784, "Overtime Report", soft.r, soft.g, soft.b);

                    iy = 758m;
                    AppendText(sb, "F2", 11m, left, iy, "Filter:", soft.r, soft.g, soft.b);
                    AppendText(sb, "F1", 11m, left + 45, iy, filterLabel, dark.r, dark.g, dark.b);
                    iy -= 15;
                    AppendText(sb, "F2", 11m, left, iy, "Generated:", soft.r, soft.g, soft.b);
                    AppendText(sb, "F1", 11m, left + 65, iy, generatedAt.ToString("dd MMM yyyy hh:mm tt"), dark.r, dark.g, dark.b);
                    iy -= 15;
                    AppendText(sb, "F2", 11m, left, iy, "Total Employees:", soft.r, soft.g, soft.b);
                    AppendText(sb, "F1", 11m, left + 105, iy, employees.Count.ToString(), dark.r, dark.g, dark.b);
                    AppendText(sb, "F2", 11m, left + 190, iy, "Avg OT Allocation:", soft.r, soft.g, soft.b);
                    AppendText(sb, "F1", 11m, left + 300, iy, summary.AverageOTAllocation.ToString("0.0") + "%", dark.r, dark.g, dark.b);
                    AppendText(sb, "F2", 11m, left + 370, iy, "Total OT Hours:", soft.r, soft.g, soft.b);
                    AppendText(sb, "F1", 11m, left + 460, iy, summary.TotalOTHours.ToString("0.0") + " hrs", dark.r, dark.g, dark.b);

                    iy -= 25m;
                }
                else
                {
                    AppendText(sb, "F2", 14m, left, 810, $"PulseHR / Overtime Report - Page {pageNumber}", dark.r, dark.g, dark.b);
                    iy = 780m;
                }

                // Render Table Header
                var headerH = 20m;
                AppendFillRect(sb, left, iy - headerH, tableWidth, headerH, hdrBg.r, hdrBg.g, hdrBg.b);
                AppendStrokeColor(sb, border.r, border.g, border.b);
                AppendStrokeRect(sb, left, iy - headerH, tableWidth, headerH, border.r, border.g, border.b);

                foreach (var cx in new[] { c1, c2, c3, c4 })
                    sb.AppendLine(FormattableString.Invariant($"{cx} {iy} m {cx} {iy - headerH} l S"));

                AppendText(sb, "F2", 10m, left + 4, iy - 14, "Employee", hdrTxt.r, hdrTxt.g, hdrTxt.b);
                AppendText(sb, "F2", 10m, c1 + 4, iy - 14, "Department", hdrTxt.r, hdrTxt.g, hdrTxt.b);
                AppendText(sb, "F2", 10m, c2 + 4, iy - 14, "OT Hours", hdrTxt.r, hdrTxt.g, hdrTxt.b);
                AppendText(sb, "F2", 10m, c3 + 4, iy - 14, "Status", hdrTxt.r, hdrTxt.g, hdrTxt.b);
                AppendText(sb, "F2", 10m, c4 + 4, iy - 14, "Task Description", hdrTxt.r, hdrTxt.g, hdrTxt.b);

                iy -= headerH;
            }

            StartNewPageHeader(true);

            if (!employees.Any())
            {
                var emptyH = 24m;
                AppendStrokeRect(sb, left, iy - emptyH, tableWidth, emptyH, border.r, border.g, border.b);
                AppendText(sb, "F1", 10m, left + 4, iy - 16, "No employees match the selected filter.", dark.r, dark.g, dark.b);
                iy -= emptyH;
            }
            else
            {
                foreach (var emp in employees)
                {
                    var tasks = emp.DailyTasks != null && emp.DailyTasks.Any()
                        ? emp.DailyTasks
                        : new List<OTDailyTaskViewModel>();

                    var taskLinesCount = Math.Max(1, tasks.Count);
                    var rowH = Math.Max(24m, taskLinesCount * 14m + 8m);

                    // Check page break condition
                    if (iy - rowH < 90m)
                    {
                        pageStreams.Add(sb.ToString());
                        pageNumber++;
                        StartNewPageHeader(false);
                    }

                    var rowTop = iy;
                    var rowBot = iy - rowH;

                    // Draw outer border and vertical column lines
                    AppendStrokeRect(sb, left, rowBot, tableWidth, rowH, border.r, border.g, border.b);
                    foreach (var cx in new[] { c1, c2, c3, c4 })
                        sb.AppendLine(FormattableString.Invariant($"{cx} {rowTop} m {cx} {rowBot} l S"));

                    // Employee Name & Service ID
                    var empName = $"{emp.FirstName} {emp.LastName}";
                    if (empName.Length > 20) empName = empName.Substring(0, 18) + "..";
                    AppendText(sb, "F2", 9.5m, left + 4, rowTop - 13, empName, dark.r, dark.g, dark.b);
                    AppendText(sb, "F1", 8.5m, left + 4, rowTop - 24, $"ID: {emp.ServiceId ?? "-"}", soft.r, soft.g, soft.b);

                    // Department
                    var dept = emp.Department ?? "Not Assigned";
                    if (dept.Length > 15) dept = dept.Substring(0, 13) + "..";
                    AppendText(sb, "F1", 9m, c1 + 4, rowTop - 14, dept, dark.r, dark.g, dark.b);

                    // OT Hours
                    AppendText(sb, "F2", 9.5m, c2 + 4, rowTop - 14, $"{emp.TotalOTHours:0.0} hrs", dark.r, dark.g, dark.b);

                    // Status
                    AppendText(sb, "F1", 9m, c3 + 4, rowTop - 14, emp.Status, dark.r, dark.g, dark.b);

                    // Daily Task Descriptions
                    if (!tasks.Any())
                    {
                        AppendText(sb, "F1", 8.5m, c4 + 4, rowTop - 14, "No description provided", soft.r, soft.g, soft.b);
                    }
                    else
                    {
                        var taskY = rowTop - 13m;
                        foreach (var task in tasks)
                        {
                            var desc = task.TaskDescription;
                            if (desc.Length > 32) desc = desc.Substring(0, 30) + "..";
                            var taskLine = $"{task.Date:dd/MM}: {desc} ({task.OTHours:0.0}h)";
                            AppendText(sb, "F1", 8.5m, c4 + 4, taskY, taskLine, dark.r, dark.g, dark.b);
                            taskY -= 14m;
                        }
                    }

                    iy = rowBot;
                }
            }

            // Summary box at end of report
            var totH = 36m;
            if (iy - totH < 40m)
            {
                pageStreams.Add(sb.ToString());
                pageNumber++;
                StartNewPageHeader(false);
            }

            iy -= 15m;
            var totY = iy - totH;
            AppendFillRect(sb, left, totY, tableWidth, totH, totBg.r, totBg.g, totBg.b);
            AppendStrokeRect(sb, left, totY, tableWidth, totH, border.r, border.g, border.b);

            AppendText(sb, "F2", 10m, left + 8, totY + 22, "Above 100%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 10m, left + 75, totY + 22, summary.EmployeesAbove100.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 10m, left + 115, totY + 22, "95-100%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 10m, left + 165, totY + 22, summary.Employees95to100.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 10m, left + 205, totY + 22, "80-95%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 10m, left + 250, totY + 22, summary.Employees80to95.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 10m, left + 290, totY + 22, "Below 80%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 10m, left + 355, totY + 22, summary.EmployeesBelow80.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 10m, left + 395, totY + 22, "Total Employees:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 10m, left + 485, totY + 22, summary.TotalEmployees.ToString(), purple.r, purple.g, purple.b);

            pageStreams.Add(sb.ToString());
            return pageStreams;
        }

        private static byte[] BuildMultiPagePdf(List<string> pageStreams)
        {
            var objects = new List<string>();

            int pageCount = pageStreams.Count;
            int fontF1ObjNum = 3 + pageCount;
            int fontF2ObjNum = 4 + pageCount;
            int firstStreamObjNum = 5 + pageCount;

            var kidsRef = new StringBuilder();
            for (int i = 0; i < pageCount; i++)
            {
                kidsRef.Append($"{3 + i} 0 R ");
            }

            // Obj 1: Catalog
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");

            // Obj 2: Pages
            objects.Add($"<< /Type /Pages /Kids [{kidsRef.ToString().Trim()}] /Count {pageCount} >>");

            // Obj 3 to 3 + pageCount - 1: Page objects
            for (int i = 0; i < pageCount; i++)
            {
                int streamObjNum = firstStreamObjNum + i;
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontF1ObjNum} 0 R /F2 {fontF2ObjNum} 0 R >> >> /Contents {streamObjNum} 0 R >>");
            }

            // Font F1 & F2
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

            // Streams
            for (int i = 0; i < pageCount; i++)
            {
                var bytes = Encoding.ASCII.GetBytes(pageStreams[i]);
                objects.Add($"<< /Length {bytes.Length} >>\nstream\n{pageStreams[i]}\nendstream");
            }

            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.ASCII, 1024, leaveOpen: true);
            writer.Write("%PDF-1.4\n");
            writer.Flush();

            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(ms.Position);
                writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
                writer.Flush();
            }

            var xrefOffset = ms.Position;
            writer.Write($"xref\n0 {objects.Count + 1}\n");
            writer.Write("0000000000 65535 f \n");
            for (var i = 1; i < offsets.Count; i++)
            {
                writer.Write($"{offsets[i]:0000000000} 00000 n \n");
            }

            writer.Write("trailer\n");
            writer.Write($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            writer.Write("startxref\n");
            writer.Write($"{xrefOffset}\n");
            writer.Write("%%EOF");
            writer.Flush();

            return ms.ToArray();
        }

        private static void AppendStrokeColor(StringBuilder sb, decimal r, decimal g, decimal b)
        {
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} RG"));
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} rg"));
        }

        private static void AppendFillRect(StringBuilder sb, decimal x, decimal y, decimal w, decimal h, decimal r, decimal g, decimal b)
        {
            sb.AppendLine("q");
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} rg"));
            sb.AppendLine(FormattableString.Invariant($"{x} {y} {w} {h} re f"));
            sb.AppendLine("Q");
        }

        private static void AppendStrokeRect(StringBuilder sb, decimal x, decimal y, decimal w, decimal h, decimal r, decimal g, decimal b)
        {
            sb.AppendLine("q");
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} RG"));
            sb.AppendLine(FormattableString.Invariant($"{x} {y} {w} {h} re S"));
            sb.AppendLine("Q");
        }

        private static void AppendText(StringBuilder sb, string font, decimal size, decimal x, decimal y, string text, decimal r, decimal g, decimal b)
        {
            var escaped = EscapePdf(text);
            sb.AppendLine("BT");
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} rg"));
            sb.AppendLine(FormattableString.Invariant($"/{font} {size} Tf"));
            sb.AppendLine(FormattableString.Invariant($"{x} {y} Td"));
            sb.AppendLine($"({escaped}) Tj");
            sb.AppendLine("ET");
        }

        private static string EscapePdf(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                var c = ch > 255 ? '?' : ch;
                sb.Append(c switch { '\\' => "\\\\", '(' => "\\(", ')' => "\\)", '\n' => " ", '\r' => " ", _ => c.ToString() });
            }
            return sb.ToString();
        }
    }
}
