using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eArchiveSystem.Application.Interfaces.Services;

namespace eArchiveSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/ocr")]
    public class OcrCallbackController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly IMetadataRepository _metadata;
        private readonly IRuleBasedAnalyzer _analyzer;
        private readonly ITextPreprocessorService _preprocessor;
        private readonly IIndexingService _indexingService;

        public OcrCallbackController(
            IDocumentRepository documents,
            IMetadataRepository metadata,
             IRuleBasedAnalyzer analyzer,
              ITextPreprocessorService preprocessor,
              IIndexingService indexingService)
        {
            _documents = documents;
            _metadata = metadata;
            _analyzer = analyzer;
            _preprocessor = preprocessor;
            _indexingService = indexingService;
        }
        [AllowAnonymous]
        [HttpPost("callback")]
        public async Task<IActionResult> ReceiveResult(
[FromQuery] string documentId,
[FromBody] OcrCallbackDto result)
        {
            var doc = await _documents.GetByIdAsync(documentId);
            if (doc == null)
                return NotFound();

            //  Preprocessing
            var cleaned = _preprocessor.Clean(result.Text);

            // Analyzer on cleaned text
            var description = _analyzer.ExtractDescription(cleaned);
            var tags = _analyzer.ExtractKeywords(cleaned);
            var category = _analyzer.DetectCategory(cleaned);
            var documentType = _analyzer.DetectDocumentType(cleaned);

            // 1️⃣ Save metadata in Metadata collection
            var metadata = new Metadata
            {
                Id = documentId,
                Description = description,
                Category = category,
                DocumentType = documentType,
                Tags = tags,
                Department = doc.Department,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _metadata.UpsertAsync(metadata);

            // 2️⃣ Embed metadata into Document
            await _documents.AttachMetadataAsync(documentId);

            // 3️⃣ Save cleaned content (جاهز للبحث)
            await _documents.UpdateContentAsync(
                documentId,
                cleaned,
                doc.Department
            );

            await _indexingService.SyncDocumentAsync(documentId);

            return Ok("OCR processed successfully");
        }

    }
}
