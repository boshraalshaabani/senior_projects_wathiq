using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace eArchiveSystem.Presentation.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IMetadataService _metadataService;
        private readonly ISearchService _searchService;

        public DocumentController(
            IDocumentService documentService,
            IMetadataService metadataService,
            ISearchService searchService)
        {
            _documentService = documentService;
            _metadataService = metadataService;
            _searchService = searchService;
        }

        [Authorize(Roles = "Manager,Employee")]
        [HttpPost("Add")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] AddDocumentDto dto)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var result = await _documentService.AddDocumentAsync(currentUserId, dto);

            if (result.IsDuplicate)
            {
                return Conflict(new
                {
                    message = result.Message,
                    existingDocumentId = result.Document.Id,
                    existingTitle = result.Document.Title
                });
            }

            return Ok(new
            {
                message = result.Message,
                document = new
                {
                    id = result.Document.Id,
                    title = result.Document.Title,
                    fileName = result.Document.FileName,
                    size = result.Document.Size
                }
            });
        }

        [Authorize(Roles = "Manager,Employee")]
        [HttpPost("{id}/metadata")]
        public async Task<IActionResult> AddMetadata(string id, [FromBody] AddMetadataDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var ok = await _metadataService.AddMetadataAsync(id, dto, userId, role);

            if (!ok)
                return Forbid();

            return Ok(new { message = "Metadata added" });
        }

        [Authorize(Roles = "Manager,Employee")]
        [HttpPut("{id}/metadata")]
        public async Task<IActionResult> UpdateMetadata(string id, [FromBody] AddMetadataDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var ok = await _metadataService.UpdateMetadataAsync(id, dto, userId, role);

            if (!ok)
                return Forbid();

            return Ok(new { message = "Metadata updated" });
        }

        [Authorize(Roles = "SystemAdmin,InstitutionAdmin,Manager,Employee")]
        [HttpPost("search")]
        public async Task<IActionResult> SearchDocuments(SearchDocumentsDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var result = await _searchService.SearchDocumentsAsync(dto, userId, role);
            return Ok(result);
        }

        [Authorize(Roles = "Manager,Employee")]
        [HttpDelete("{documentId}")]
        public async Task<IActionResult> DeleteDocument(string documentId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            await _documentService.DeleteDocumentAsync(documentId, userId, role);
            return Ok(new { message = "Document deleted successfully" });
        }

        [HttpGet("{id}/view")]
        [Authorize(Roles = "SystemAdmin,InstitutionAdmin,Manager,Employee")]
        public async Task<IActionResult> View(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;
            var doc = await _documentService.ViewDocumentAsync(id, userId, role);
            return Ok(doc);
        }

        [Authorize(Roles = "SystemAdmin,InstitutionAdmin,Manager,Employee")]
        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;
            var result = await _documentService.DownloadDocumentAsync(id, userId, role);

            return File(
                result.FileStream,
                result.ContentType,
                result.FileName);
        }

        [Authorize(Roles = "SystemAdmin,InstitutionAdmin,Manager,Employee")]
        [HttpGet("{id}/metadata")]
        public async Task<IActionResult> ViewMetadata(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var meta = await _metadataService.ViewMetadataAsync(id, userId, role);

            if (meta == null)
            {
                return Accepted(new
                {
                    status = "processing",
                    message = "OCR is still processing"
                });
            }

            return Ok(meta);
        }

        [Authorize(Roles = "Manager,Employee")]
        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateDocument(string id, [FromForm] UpdateDocumentDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var result = await _documentService.UpdateDocumentAsync(id, dto, userId, role);

            return Ok(new
            {
                message = result.Message,
                document = new
                {
                    id = result.Document.Id,
                    title = result.Document.Title,
                    fileName = result.Document.FileName,
                    size = result.Document.Size
                }
            });
        }
    }
}
