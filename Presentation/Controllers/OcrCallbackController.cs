using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Persistence;
using eArchiveSystem.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eArchiveSystem.Application.Interfaces.Services;
using System.Text.Json;

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
        private readonly IOcrExtractionAssessmentService _assessmentService;

        public OcrCallbackController(
            IDocumentRepository documents,
            IMetadataRepository metadata,
             IRuleBasedAnalyzer analyzer,
              ITextPreprocessorService preprocessor,
              IIndexingService indexingService,
              IOcrExtractionAssessmentService assessmentService)
        {
            _documents = documents;
            _metadata = metadata;
            _analyzer = analyzer;
            _preprocessor = preprocessor;
            _indexingService = indexingService;
            _assessmentService = assessmentService;
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
            var description = result.StructuredData?.Summary ?? _analyzer.ExtractDescription(cleaned);
            var tags = result.StructuredData?.Keywords ?? _analyzer.ExtractKeywords(cleaned);
            var category = _analyzer.DetectCategory(cleaned);
            var documentType = _analyzer.DetectDocumentType(cleaned, doc.FileName);
            var issuingEntity = result.StructuredData?.IssuingEntity ?? _analyzer.DetectIssuingEntity(cleaned);
            var referenceNumber = result.StructuredData?.ReferenceNumber ?? _analyzer.ExtractReferenceNumber(cleaned);
            var documentDate = result.StructuredData?.DocumentDate ?? _analyzer.ExtractDocumentDate(cleaned);
            var insights = result.StructuredData?.Insights ?? _analyzer.ExtractInsights(cleaned);
            var headers = result.StructuredData?.Headers ?? _analyzer.ExtractHeaders(result.Text);
            var footers = result.StructuredData?.Footers ?? _analyzer.ExtractFooters(result.Text);
            var stamps = result.StructuredData?.Stamps ?? _analyzer.DetectStamps(result.Text);
            var signatures = result.StructuredData?.Signatures ?? _analyzer.DetectSignatures(result.Text);
            var hasSignature = result.StructuredData?.HasSignature ?? signatures.Count > 0;
            var rawExtractionJson = result.RawJson;

            if (string.IsNullOrWhiteSpace(rawExtractionJson) && result.StructuredData != null)
                rawExtractionJson = JsonSerializer.Serialize(result.StructuredData);

            var structuredDataProvided = result.StructuredData != null || !string.IsNullOrWhiteSpace(result.RawJson);

            // 1️⃣ Save metadata in Metadata collection
            var metadata = new Metadata
            {
                Id = documentId,
                Description = description,
                Category = category,
                DocumentType = documentType,
                Tags = tags,
                IssuingEntity = issuingEntity,
                ReferenceNumber = referenceNumber,
                DocumentDate = documentDate,
                Insights = insights,
                HasSignature = hasSignature,
                Signatures = signatures,
                Headers = headers,
                Footers = footers,
                Stamps = stamps,
                RawExtractionJson = rawExtractionJson,
                Department = doc.Department,
                DepartmentId = doc.DepartmentId ?? doc.Department,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var assessment = _assessmentService.Assess(metadata, structuredDataProvided);
            metadata.StructuredDataProvided = assessment.StructuredDataProvided;
            metadata.CoreFieldsComplete = assessment.CoreFieldsComplete;
            metadata.AdvancedMetadataComplete = assessment.AdvancedMetadataComplete;
            metadata.LayoutAnalysisAvailable = assessment.LayoutAnalysisAvailable;
            metadata.RequiresReview = assessment.RequiresReview;
            metadata.ExtractionStatus = assessment.ExtractionStatus;
            metadata.MissingFields = assessment.MissingFields;

            await _metadata.UpsertAsync(metadata);

            // 2️⃣ Embed metadata into Document
            await _documents.AttachMetadataAsync(documentId);

            // 3️⃣ Save cleaned content (جاهز للبحث)
            await _documents.UpdateContentAsync(
                documentId,
                cleaned,
                doc.Department,
                doc.DepartmentId ?? doc.Department
            );

            await _indexingService.SyncDocumentAsync(documentId);

            await _documents.UpdateStatusAsync(documentId, DocumentStatus.Draft);

            return Ok("OCR processed successfully");
        }

    }
}
