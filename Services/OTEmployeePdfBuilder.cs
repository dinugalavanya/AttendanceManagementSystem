using System.Globalization;
using System.Text;
using AttendanceManagementSystem.ViewModels;

namespace AttendanceManagementSystem.Services
{
    public static class OTEmployeePdfBuilder
    {
        public static byte[] Build(List<OTEmployeeViewModel> employees, string filterLabel, OTSummaryViewModel summary, DateTime generatedAt)
        {
            var content = BuildContent(employees, filterLabel, summary, generatedAt);
            return BuildPdf(content);
        }

        private static string BuildContent(List<OTEmployeeViewModel> employees, string filterLabel, OTSummaryViewModel summary, DateTime generatedAt)
        {
            var sb = new StringBuilder();
            var left = 30m; var right = 565m; var tableWidth = right - left;
            var rowH = 18m;

            var dark = (r: 0.067m, g: 0.094m, b: 0.153m);
            var soft = (r: 0.216m, g: 0.255m, b: 0.318m);
            var hdrTxt = (r: 0.118m, g: 0.227m, b: 0.541m);
            var hdrBg = (r: 0.933m, g: 0.957m, b: 1.0m);
            var border = (r: 0.796m, g: 0.835m, b: 0.882m);
            var purple = (r: 0.486m, g: 0.227m, b: 0.929m);
            var totBg = (r: 0.972m, g: 0.980m, b: 1.0m);

            AppendText(sb, "F2", 22m, left, 810, "PulseHR / OT Management System", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 15m, left, 784, "OT Allocation Report", soft.r, soft.g, soft.b);

            var iy = 758m;
            AppendText(sb, "F2", 11m, left, iy, "Filter:", soft.r, soft.g, soft.b);
            AppendText(sb, "F1", 11m, left + 50, iy, filterLabel, dark.r, dark.g, dark.b);
            iy -= 15;
            AppendText(sb, "F2", 11m, left, iy, "Generated:", soft.r, soft.g, soft.b);
            AppendText(sb, "F1", 11m, left + 70, iy, generatedAt.ToString("dd MMM yyyy hh:mm tt"), dark.r, dark.g, dark.b);
            iy -= 15;
            AppendText(sb, "F2", 11m, left, iy, "Total Employees:", soft.r, soft.g, soft.b);
            AppendText(sb, "F1", 11m, left + 105, iy, employees.Count.ToString(), dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 11m, left + 200, iy, "Avg OT Allocation:", soft.r, soft.g, soft.b);
            AppendText(sb, "F1", 11m, left + 310, iy, summary.AverageOTAllocation.ToString("0.0") + "%", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 11m, left + 380, iy, "Total OT Hours:", soft.r, soft.g, soft.b);
            AppendText(sb, "F1", 11m, left + 470, iy, summary.TotalOTHours.ToString("0.0") + " hrs", dark.r, dark.g, dark.b);

            var tableTop = iy - 20;
            var rowCount = Math.Max(1, employees.Count);
            var tableH = rowH * (rowCount + 1);
            var tableBot = tableTop - tableH;

            AppendFillRect(sb, left, tableTop - rowH, tableWidth, rowH, hdrBg.r, hdrBg.g, hdrBg.b);
            AppendStrokeColor(sb, border.r, border.g, border.b);
            sb.AppendLine(FormattableString.Invariant($"{left} {tableTop} m {right} {tableTop} l S"));
            sb.AppendLine(FormattableString.Invariant($"{left} {tableBot} m {right} {tableBot} l S"));
            sb.AppendLine(FormattableString.Invariant($"{left} {tableTop} m {left} {tableBot} l S"));
            sb.AppendLine(FormattableString.Invariant($"{right} {tableTop} m {right} {tableBot} l S"));

            var c1 = left + tableWidth * 0.22m;
            var c2 = left + tableWidth * 0.38m;
            var c3 = left + tableWidth * 0.58m;
            var c4 = left + tableWidth * 0.76m;
            foreach (var cx in new[] { c1, c2, c3, c4 })
                sb.AppendLine(FormattableString.Invariant($"{cx} {tableTop} m {cx} {tableBot} l S"));

            AppendText(sb, "F2", 10m, left + 4, tableTop - 12, "Employee", hdrTxt.r, hdrTxt.g, hdrTxt.b);
            AppendText(sb, "F2", 10m, c1 + 4, tableTop - 12, "Service ID", hdrTxt.r, hdrTxt.g, hdrTxt.b);
            AppendText(sb, "F2", 10m, c2 + 4, tableTop - 12, "Department", hdrTxt.r, hdrTxt.g, hdrTxt.b);
            AppendText(sb, "F2", 10m, c3 + 4, tableTop - 12, "OT Allocation %", hdrTxt.r, hdrTxt.g, hdrTxt.b);
            AppendText(sb, "F2", 10m, c4 + 4, tableTop - 12, "OT Hours / Status", hdrTxt.r, hdrTxt.g, hdrTxt.b);

            var rowY = tableTop - rowH;
            if (!employees.Any())
            {
                rowY -= rowH;
                sb.AppendLine(FormattableString.Invariant($"{left} {rowY + rowH} m {right} {rowY + rowH} l S"));
                AppendText(sb, "F1", 10m, left + 4, rowY + 5, "No employees match the selected filter.", dark.r, dark.g, dark.b);
            }
            else
            {
                foreach (var emp in employees)
                {
                    rowY -= rowH;
                    sb.AppendLine(FormattableString.Invariant($"{left} {rowY + rowH} m {right} {rowY + rowH} l S"));
                    AppendText(sb, "F1", 10m, left + 4, rowY + 5, $"{emp.FirstName} {emp.LastName}", dark.r, dark.g, dark.b);
                    AppendText(sb, "F1", 10m, c1 + 4, rowY + 5, emp.ServiceId ?? "-", dark.r, dark.g, dark.b);
                    AppendText(sb, "F1", 10m, c2 + 4, rowY + 5, emp.Department, dark.r, dark.g, dark.b);
                    AppendText(sb, "F1", 10m, c3 + 4, rowY + 5, emp.OTAllocationPercentage.ToString("0.0") + "%", dark.r, dark.g, dark.b);
                    AppendText(sb, "F1", 10m, c4 + 4, rowY + 5, emp.TotalOTHours.ToString("0.0") + " hrs | " + emp.Status, dark.r, dark.g, dark.b);
                }
            }

            var totY = Math.Max(60m, tableBot - 50m);
            AppendFillRect(sb, left, totY, tableWidth, 36m, totBg.r, totBg.g, totBg.b);
            AppendStrokeRect(sb, left, totY, tableWidth, 36m, border.r, border.g, border.b);
            AppendText(sb, "F2", 11m, left + 8, totY + 22, "Above 100%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 11m, left + 80, totY + 22, summary.EmployeesAbove100.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 11m, left + 130, totY + 22, "95-100%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 11m, left + 190, totY + 22, summary.Employees95to100.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 11m, left + 230, totY + 22, "80-95%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 11m, left + 280, totY + 22, summary.Employees80to95.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 11m, left + 320, totY + 22, "Below 80%:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 11m, left + 390, totY + 22, summary.EmployeesBelow80.ToString(), purple.r, purple.g, purple.b);
            AppendText(sb, "F2", 11m, left + 430, totY + 22, "Total:", dark.r, dark.g, dark.b);
            AppendText(sb, "F2", 11m, left + 465, totY + 22, summary.TotalEmployees.ToString(), purple.r, purple.g, purple.b);

            return sb.ToString();
        }

        private static byte[] BuildPdf(string contentStream)
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
            writer.Write("%PDF-1.4\n"); writer.Flush();
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
                writer.Write($"{offsets[i]:0000000000} 00000 n \n");
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
