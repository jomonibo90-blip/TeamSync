using System.Text;
using TeamSync.Models;
using System.Collections.Generic;

namespace TeamSync.Services
{
    public static class CsvExportService
    {
        // Generate CSV bytes with UTF-8 BOM for compatibility with Excel
        public static byte[] GenerateContributionsCsvBytes(IEnumerable<Contribution> contributions, Models.Task? task = null, Models.Group? group = null)
        {
            static string EscapeCsv(string? input)
            {
                if (string.IsNullOrEmpty(input)) return "";
                var escaped = input.Replace("\"", "\"\"");
                return $"\"{escaped}\"";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Id,GroupName,TaskId,TaskTitle,UserName,UserEmail,RecordedByName,RecordedByEmail,ContributedAt,Hours,Description,Notes,Source,RecordType,IsStudentSubmitted,OverrideJustification");

            foreach (var c in contributions)
            {
                var userName = c.User != null ? (c.User.FirstName + " " + c.User.LastName).Trim() : c.UserId;
                var userEmail = c.User?.Email ?? string.Empty;
                var recordedByName = c.RecordedBy != null ? (c.RecordedBy.FirstName + " " + c.RecordedBy.LastName).Trim() : (c.RecordedById ?? string.Empty);
                var recordedByEmail = c.RecordedBy?.Email ?? string.Empty;

                var groupName = group?.Name ?? (c.Task?.Group?.Name ?? string.Empty);
                var taskTitle = task?.Title ?? c.Task?.Title ?? string.Empty;
                var taskId = task?.Id ?? c.TaskId;

                // Original contribution row
                var fields = new[]
                {
                    c.Id.ToString(),
                    EscapeCsv(groupName),
                    taskId.ToString(),
                    EscapeCsv(taskTitle),
                    EscapeCsv(userName),
                    EscapeCsv(userEmail),
                    EscapeCsv(recordedByName),
                    EscapeCsv(recordedByEmail),
                    EscapeCsv(c.ContributedAt.ToString("o")),
                    EscapeCsv(c.HoursSpent?.ToString() ?? string.Empty),
                    EscapeCsv(c.Description ?? string.Empty),
                    EscapeCsv(c.Notes ?? string.Empty),
                    EscapeCsv(c.Source ?? string.Empty),
                    "Original",
                    c.IsStudentSubmitted ? "Yes" : "No",
                    ""
                };

                sb.AppendLine(string.Join(",", fields));

                // Override rows (if any)
                if (c.Overrides != null && c.Overrides.Any())
                {
                    foreach (var ovr in c.Overrides.OrderByDescending(o => o.OverriddenAt))
                    {
                        var overriddenByName = ovr.OverriddenBy != null ? (ovr.OverriddenBy.FirstName + " " + ovr.OverriddenBy.LastName).Trim() : (ovr.OverriddenById ?? string.Empty);
                        var overriddenByEmail = ovr.OverriddenBy?.Email ?? string.Empty;

                        var overrideFields = new[]
                        {
                            ovr.Id.ToString(),
                            EscapeCsv(groupName),
                            taskId.ToString(),
                            EscapeCsv(taskTitle),
                            EscapeCsv(userName),
                            EscapeCsv(userEmail),
                            EscapeCsv(overriddenByName),
                            EscapeCsv(overriddenByEmail),
                            EscapeCsv(ovr.OverriddenAt.ToString("o")),
                            EscapeCsv(ovr.NewHours?.ToString() ?? string.Empty),
                            EscapeCsv(ovr.NewDescription ?? string.Empty),
                            "", // Notes not applicable to overrides
                            EscapeCsv("Override"),
                            "Override",
                            "", // IsStudentSubmitted not applicable to overrides
                            EscapeCsv(ovr.Justification ?? string.Empty)
                        };

                        sb.AppendLine(string.Join(",", overrideFields));
                    }
                }
            }

            var utf8Preamble = Encoding.UTF8.GetPreamble();
            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var bytes = new byte[utf8Preamble.Length + csvBytes.Length];
            Buffer.BlockCopy(utf8Preamble, 0, bytes, 0, utf8Preamble.Length);
            Buffer.BlockCopy(csvBytes, 0, bytes, utf8Preamble.Length, csvBytes.Length);
            return bytes;
        }
    }
}
