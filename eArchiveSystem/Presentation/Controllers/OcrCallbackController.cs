using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eArchiveSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/ocr")]
    public class OcrCallbackController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ITextPreprocessorService _preprocessor;
        private readonly IIndexingService _indexingService;

        public OcrCallbackController(
            IDocumentRepository documents,
            ITextPreprocessorService preprocessor,
            IIndexingService indexingService)
        {
            _documents = documents;
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

            var normalizedText = string.IsNullOrWhiteSpace(result.NormalizedText)
                ? result.Text
                : result.NormalizedText;

            var rawText = string.IsNullOrWhiteSpace(result.RawText)
                ? normalizedText
                : result.RawText;

            var cleaned = _preprocessor.Clean(normalizedText);

            await _documents.UpdateContentAsync(
                documentId,
                cleaned,
                rawText,
                normalizedText,
                result.Provider,
                result.Language,
                result.Pages,
                doc.Department,
                doc.DepartmentId ?? doc.Department
            );

            // Move the document out of Processing as soon as OCR text is safely stored.
            await _documents.UpdateStatusAsync(documentId, DocumentStatus.Draft);

            // Search indexing can run after the status change without blocking manual metadata save.
            await _indexingService.SyncDocumentAsync(documentId);

            return Ok("OCR text stored successfully");
        }
    }
}
