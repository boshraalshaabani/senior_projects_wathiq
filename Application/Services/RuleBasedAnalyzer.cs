using System.Text.RegularExpressions;
using eArchiveSystem.Application.Interfaces.Services;

namespace eArchiveSystem.Application.Services
{
    public class RuleBasedAnalyzer : IRuleBasedAnalyzer
    {
        private static readonly string[] StopWords =
        {
            "the","and","of","in","to","is","for","on","with","as","by"
        };

        public string ExtractDescription(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var sentences = Regex.Split(text, @"(?<=[\.])")
                .Select(s => s.Trim())
                .Where(s => s.Length > 30)
                .Take(2);

            return string.Join(" ", sentences);
        }

        public List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return text.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4 && !StopWords.Contains(w))
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => g.Key)
                .ToList();
        }

        public string DetectCategory(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "General";

            text = text.ToLower();

            if (text.Contains("lecture") || text.Contains("assignment"))
                return "Academic";

            if (text.Contains("policy") || text.Contains("employee"))
                return "HR";

            if (text.Contains("software") || text.Contains("system"))
                return "Technical";

            return "General";
        }

        public string DetectDocumentType(string text, string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "General";

            var content = text.ToLower();

            if (content.Contains("lecture") ||
                content.Contains("chapter") ||
                content.Contains("course") ||
                content.Contains("slides"))
                return "Lecture";

            if (content.Contains("assignment") ||
                content.Contains("task") ||
                content.Contains("required to") ||
                content.Contains("submit"))
                return "TaskAssignment";

            if (content.Contains("decision") ||
                content.Contains("issued by") ||
                content.Contains("effective date") ||
                content.Contains("قرار"))
                return "Decision";

            if (content.Contains("report") ||
                content.Contains("summary") ||
                content.Contains("analysis"))
                return "Report";

            if (content.Contains("contract") ||
                content.Contains("agreement") ||
                content.Contains("party") ||
                content.Contains("terms and conditions"))
                return "Contract";

            if (content.Contains("invoice") ||
                content.Contains("amount") ||
                content.Contains("total") ||
                content.Contains("vat"))
                return "Invoice";

            if (content.Contains("api") ||
                content.Contains("documentation") ||
                content.Contains("guide") ||
                content.Contains("installation"))
                return "TechnicalGuide";

            return "General";
        }
    }
}
