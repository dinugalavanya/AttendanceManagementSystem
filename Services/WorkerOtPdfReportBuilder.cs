using System.Globalization;
using System.Text;
using AttendanceManagementSystem.ViewModels;

namespace AttendanceManagementSystem.Services
{
    public static class WorkerOtPdfReportBuilder
    {
        public static byte[] BuildRecentOtRecordsPdf(WorkerOtDashboardViewModel model, DateTime generatedAt)
        {
            var content = BuildPdfContentStream(model, generatedAt);
            return BuildSinglePagePdf(content);
        }

        private static string BuildPdfContentStream(WorkerOtDashboardViewModel model, DateTime generatedAt)
        {
            var sb = new StringBuilder();
            var rowHeight = 20m;
            var left = 30m;
            var right = 565m;
            var tableWidth = right - left;

            // Strong, print-friendly palette for PDF only (no muted dashboard styling).
            var textMain = (r: 0.0667m, g: 0.0941m, b: 0.1529m);      // #111827
            var textSecondary = (r: 0.2157m, g: 0.2549m, b: 0.3176m); // #374151
            var tableHeaderText = (r: 0.1176m, g: 0.2275m, b: 0.5412m); // #1e3a8a
            var tableHeaderBg = (r: 0.9333m, g: 0.9569m, b: 1.0m);    // #eef4ff
            var tableBorder = (r: 0.7961m, g: 0.8353m, b: 0.8824m);   // #cbd5e1
            var totalValuePurple = (r: 0.4863m, g: 0.2275m, b: 0.9294m); // #7c3aed
            var totalBoxBg = (r: 0.9725m, g: 0.9804m, b: 1.0m);       // #f8faff

            AppendText(sb, "F2", 24m, left, 810, "PulseHR / OT Management System", textMain.r, textMain.g, textMain.b);
            AppendText(sb, "F2", 17m, left, 784, "Recent OT Records", textSecondary.r, textSecondary.g, textSecondary.b);

            var infoY = 756m;
            AppendText(sb, "F2", 12m, left, infoY, "Worker Name:", textSecondary.r, textSecondary.g, textSecondary.b);
            AppendText(sb, "F1", 12m, left + 92, infoY, model.FullName, textMain.r, textMain.g, textMain.b);
            infoY -= 17;
            AppendText(sb, "F2", 12m, left, infoY, "Worker Email:", textSecondary.r, textSecondary.g, textSecondary.b);
            AppendText(sb, "F1", 12m, left + 92, infoY, model.Email, textMain.r, textMain.g, textMain.b);
            infoY -= 17;
            AppendText(sb, "F2", 12m, left, infoY, "Section:", textSecondary.r, textSecondary.g, textSecondary.b);
            AppendText(sb, "F1", 12m, left + 92, infoY, model.SectionName, textMain.r, textMain.g, textMain.b);
            infoY -= 17;
            AppendText(sb, "F2", 12m, left, infoY, "Service ID:", textSecondary.r, textSecondary.g, textSecondary.b);
            AppendText(sb, "F1", 12m, left + 92, infoY, model.ServiceId, textMain.r, textMain.g, textMain.b);
            infoY -= 17;
            AppendText(sb, "F2", 12m, left, infoY, "From Date:", textSecondary.r, textSecondary.g, textSecondary.b);
            AppendText(sb, "F1", 12m, left + 92, infoY, model.MonthStartDate.ToString("dd MMM yyyy"), textMain.r, textMain.g, textMain.b);
            infoY -= 17;
            AppendText(sb, "F2", 12m, left, infoY, "To Date:", textSecondary.r, textSecondary.g, textSecondary.b);
            AppendText(sb, "F1", 12m, left + 92, infoY, model.SearchDate.ToString("dd MMM yyyy"), textMain.r, textMain.g, textMain.b);
            infoY -= 17;
            AppendText(sb, "F2", 12m, left, infoY, "Search Date:", textSecondary.r, textSecondary.g, textSecondary.b);
            AppendText(sb, "F1", 12m, left + 92, infoY, model.SearchDate.ToString("dd MMM yyyy"), textMain.r, textMain.g, textMain.b);

            var tableTopY = infoY - 24;
            var rowCount = Math.Max(1, model.RecentOtRecords.Count);
            var tableHeight = rowHeight * (rowCount + 1);
            var tableBottomY = tableTopY - tableHeight;

            // Table header background
            AppendFillRect(sb, left, tableTopY - rowHeight, tableWidth, rowHeight, tableHeaderBg.r, tableHeaderBg.g, tableHeaderBg.b);

            // Table border and column lines
            AppendStrokeColor(sb, tableBorder.r, tableBorder.g, tableBorder.b);
            sb.AppendLine(FormattableString.Invariant($"{left} {tableTopY} m {right} {tableTopY} l S"));
            sb.AppendLine(FormattableString.Invariant($"{left} {tableBottomY} m {right} {tableBottomY} l S"));
            sb.AppendLine(FormattableString.Invariant($"{left} {tableTopY} m {left} {tableBottomY} l S"));
            sb.AppendLine(FormattableString.Invariant($"{right} {tableTopY} m {right} {tableBottomY} l S"));

            var c1 = left + (tableWidth * 0.28m);
            var c2 = left + (tableWidth * 0.52m);
            var c3 = left + (tableWidth * 0.76m);
            sb.AppendLine(FormattableString.Invariant($"{c1} {tableTopY} m {c1} {tableBottomY} l S"));
            sb.AppendLine(FormattableString.Invariant($"{c2} {tableTopY} m {c2} {tableBottomY} l S"));
            sb.AppendLine(FormattableString.Invariant($"{c3} {tableTopY} m {c3} {tableBottomY} l S"));

            AppendText(sb, "F2", 12m, left + 8, tableTopY - 13, "Date", tableHeaderText.r, tableHeaderText.g, tableHeaderText.b);
            AppendText(sb, "F2", 12m, c1 + 8, tableTopY - 13, "OT In Time", tableHeaderText.r, tableHeaderText.g, tableHeaderText.b);
            AppendText(sb, "F2", 12m, c2 + 8, tableTopY - 13, "OT Out Time", tableHeaderText.r, tableHeaderText.g, tableHeaderText.b);
            AppendText(sb, "F2", 12m, c3 + 8, tableTopY - 13, "OT Duration", tableHeaderText.r, tableHeaderText.g, tableHeaderText.b);

            var rowY = tableTopY - rowHeight;
            if (model.RecentOtRecords.Count == 0)
            {
                rowY -= rowHeight;
                sb.AppendLine(FormattableString.Invariant($"{left} {rowY + rowHeight} m {right} {rowY + rowHeight} l S"));
                AppendText(sb, "F1", 12m, left + 8, rowY + 6, "No OT records found for selected date range.", textMain.r, textMain.g, textMain.b);
            }
            else
            {
                foreach (var row in model.RecentOtRecords)
                {
                    rowY -= rowHeight;
                    sb.AppendLine(FormattableString.Invariant($"{left} {rowY + rowHeight} m {right} {rowY + rowHeight} l S"));
                    AppendText(sb, "F1", 12m, left + 8, rowY + 6, row.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), textMain.r, textMain.g, textMain.b);
                    AppendText(sb, "F1", 12m, c1 + 8, rowY + 6, row.OtInTime, textMain.r, textMain.g, textMain.b);
                    AppendText(sb, "F1", 12m, c2 + 8, rowY + 6, row.OtOutTime, textMain.r, textMain.g, textMain.b);
                    AppendText(sb, "F1", 12m, c3 + 8, rowY + 6, row.OtDuration, textMain.r, textMain.g, textMain.b);
                }
            }

            var totalsBoxY = Math.Max(78m, tableBottomY - 64m);
            var totalsBoxHeight = 44m;
            AppendFillRect(sb, left, totalsBoxY, tableWidth, totalsBoxHeight, totalBoxBg.r, totalBoxBg.g, totalBoxBg.b);
            AppendStrokeRect(sb, left, totalsBoxY, tableWidth, totalsBoxHeight, tableBorder.r, tableBorder.g, tableBorder.b);

            var totalsBaselineY = totalsBoxY + 28m;
            AppendText(sb, "F2", 13m, left + 10, totalsBaselineY, "Total Records:", textMain.r, textMain.g, textMain.b);
            AppendText(sb, "F2", 13m, left + 102, totalsBaselineY, model.TotalRecords.ToString(), totalValuePurple.r, totalValuePurple.g, totalValuePurple.b);

            AppendText(sb, "F2", 13m, left + 220, totalsBaselineY, "Total OT Duration:", textMain.r, textMain.g, textMain.b);
            AppendText(sb, "F2", 13m, left + 344, totalsBaselineY, model.TotalOtDurationDisplay, totalValuePurple.r, totalValuePurple.g, totalValuePurple.b);

            AppendText(sb, "F1", 12m, left, totalsBoxY - 18m, $"Generated: {generatedAt:dd MMM yyyy hh:mm tt}", textSecondary.r, textSecondary.g, textSecondary.b);

            return sb.ToString();
        }

        private static byte[] BuildSinglePagePdf(string contentStream)
        {
            var contentBytes = Encoding.ASCII.GetBytes(contentStream);
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
                $"<< /Length {contentBytes.Length} >>\nstream\n{contentStream}\nendstream"
            };

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

        private static void AppendFillRect(StringBuilder sb, decimal x, decimal y, decimal width, decimal height, decimal r, decimal g, decimal b)
        {
            sb.AppendLine("q");
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} rg"));
            sb.AppendLine(FormattableString.Invariant($"{x} {y} {width} {height} re f"));
            sb.AppendLine("Q");
        }

        private static void AppendStrokeRect(StringBuilder sb, decimal x, decimal y, decimal width, decimal height, decimal r, decimal g, decimal b)
        {
            sb.AppendLine("q");
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} RG"));
            sb.AppendLine(FormattableString.Invariant($"{x} {y} {width} {height} re S"));
            sb.AppendLine("Q");
        }

        private static void AppendText(StringBuilder sb, string fontName, decimal fontSize, decimal x, decimal y, string text, decimal r, decimal g, decimal b)
        {
            var escaped = EscapePdfText(text);
            sb.AppendLine("BT");
            sb.AppendLine(FormattableString.Invariant($"{r} {g} {b} rg"));
            sb.AppendLine(FormattableString.Invariant($"/{fontName} {fontSize} Tf"));
            sb.AppendLine(FormattableString.Invariant($"{x} {y} Td"));
            sb.AppendLine($"({escaped}) Tj");
            sb.AppendLine("ET");
        }

        private static string EscapePdfText(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var sanitized = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                var safeChar = ch > 255 ? '?' : ch;
                sanitized.Append(safeChar switch
                {
                    '\\' => "\\\\",
                    '(' => "\\(",
                    ')' => "\\)",
                    '\n' => " ",
                    '\r' => " ",
                    '\t' => " ",
                    _ => safeChar
                });
            }

            return sanitized.ToString();
        }

        public static byte[] BuildOTExportPdf(AttendanceManagementSystem.Models.User user, List<AttendanceManagementSystem.Models.Attendance> records, DateTime from, DateTime to, DateTime generatedAt)
        {
            var validDurationRecords = records
                .Where(a => a.InTime.HasValue && a.OutTime.HasValue)
                .Select(a => new
                {
                    Record = a,
                    DurationMinutes = CalculateDurationMinutes(a.InTime, a.OutTime)
                })
                .Where(x => x.DurationMinutes > 0)
                .ToList();

            var totalOtMinutes = validDurationRecords.Sum(x => x.DurationMinutes);
            var otDays = validDurationRecords.Count;
            var averageOtHoursPerDay = otDays == 0
                ? 0
                : Math.Round((totalOtMinutes / 60m) / otDays, 2);

            var model = new WorkerOtDashboardViewModel
            {
                MonthStartDate = from,
                SearchDate = to,
                Initials = BuildInitials(user.FirstName, user.LastName),
                FullName = user.FullName,
                Email = user.Email,
                SectionName = user.Section?.Name ?? "Unassigned",
                ServiceId = user.ServiceId ?? "-",
                TotalOtMinutes = totalOtMinutes,
                OtDays = otDays,
                AverageOtHoursPerDay = averageOtHoursPerDay,
                TotalRecords = records.Count,
                TotalOtDurationDisplay = FormatDurationMinutes(totalOtMinutes),
                RecentOtRecords = records
                    .OrderByDescending(a => a.AttendanceDate)
                    .ThenByDescending(a => a.OutTime)
                    .Select(a => new WorkerOtRecordRowViewModel
                    {
                        Date = a.AttendanceDate.Date,
                        OtInTime = a.InTime?.ToString(@"hh\:mm") ?? "-",
                        OtOutTime = a.OutTime?.ToString(@"hh\:mm") ?? "-",
                        OtDuration = a.InTime.HasValue && a.OutTime.HasValue
                            ? FormatDurationMinutes(CalculateDurationMinutes(a.InTime, a.OutTime))
                            : "-"
                    })
                    .ToList()
            };

            return BuildRecentOtRecordsPdf(model, generatedAt);
        }

        private static int CalculateDurationMinutes(TimeSpan? inTime, TimeSpan? outTime)
        {
            if (!inTime.HasValue || !outTime.HasValue) return 0;
            var safeOutTime = outTime.Value;
            if (safeOutTime < inTime.Value) safeOutTime = safeOutTime.Add(TimeSpan.FromDays(1));
            return Math.Max(0, (int)(safeOutTime - inTime.Value).TotalMinutes);
        }

        private static string FormatDurationMinutes(int minutes)
        {
            if (minutes <= 0) return "0h 0m";
            return $"{minutes / 60}h {minutes % 60}m";
        }

        private static string BuildInitials(string firstName, string lastName)
        {
            var firstInitial = string.IsNullOrWhiteSpace(firstName) ? 'A' : char.ToUpperInvariant(firstName.Trim()[0]);
            var lastInitial = string.IsNullOrWhiteSpace(lastName) ? 'U' : char.ToUpperInvariant(lastName.Trim()[0]);
            return $"{firstInitial}{lastInitial}";
        }
    }
}
