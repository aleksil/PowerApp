using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PowerApp.Models
{
    public class PowerSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsElevated { get; set; }
        public string? ErrorMessage { get; set; }
        public List<PowerCategoryGroup> Categories { get; set; } = new();

        public int TotalActiveRequests => Categories.Sum(c => c.ActiveCount);
        public bool HasActiveRequests => TotalActiveRequests > 0;

        public string SummaryText
        {
            get
            {
                if (!string.IsNullOrEmpty(ErrorMessage))
                    return $"Error: {ErrorMessage}";

                if (!HasActiveRequests)
                    return "No wake requests (Sleep allowed)";

                var activeCategories = Categories
                    .Where(c => c.HasActiveRequests)
                    .Select(c => $"{c.Title}: {c.ActiveCount}");

                return $"Wake Requests Active ({string.Join(", ", activeCategories)})";
            }
        }

        public string ToPowercfgOutput()
        {
            var sb = new StringBuilder();
            foreach (var group in Categories)
            {
                sb.AppendLine($"{group.Title}:");
                if (group.Requests.Count == 0)
                {
                    sb.AppendLine("None.");
                }
                else
                {
                    foreach (var req in group.Requests)
                    {
                        var times = req.Count > 1 ? $"[{req.Count} times] " : "";
                        var details = !string.IsNullOrEmpty(req.Details) ? $" ({req.Details})" : "";
                        sb.AppendLine($"[{req.RequesterTypeName}] {times}{req.Name}{details}");
                        if (!string.IsNullOrWhiteSpace(req.Reason))
                        {
                            sb.AppendLine(req.Reason);
                        }
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
