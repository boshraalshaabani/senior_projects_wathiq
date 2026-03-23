
        namespace eArchiveSystem.Application.Interfaces.Services
    {
        public interface IRuleBasedAnalyzer
        {
            string ExtractDescription(string text);
            List<string> ExtractKeywords(string text);
            string DetectCategory(string text);
            string DetectDocumentType(string text, string? fileName = null);
        }
    }


