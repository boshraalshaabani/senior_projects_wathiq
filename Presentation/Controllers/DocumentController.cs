using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Application.Interfaces.Services;
using eArchiveSystem.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Net.Http;
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
        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _env;


        public DocumentController(IDocumentService documentService,
                                  IMetadataService metadataService,
                                   ISearchService searchService,
                                    HttpClient httpClient,
                                    IWebHostEnvironment env
                                   )
        {
            _documentService = documentService; // Add + Get
            _metadataService = metadataService; // Metadata
            _searchService = searchService;
            _httpClient = httpClient;
            _env = env;
        }


        // Upload Document + Duplication Handling
        [Authorize(Roles = "User,Manager")]
        // (User / Manager) يقدر يرفع
        [HttpPost("Add")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] AddDocumentDto dto)
        {
            //يعرف مين رفع
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            //يعرف دوره
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            string ownerId = currentUserId; // الافتراضي

            //  VALIDATION 1:
            // User لا يحق له استخدام TargetUserId
            if (role == "User" && !string.IsNullOrEmpty(dto.TargetUserId))
            {
                return BadRequest(new
                {
                    message = "You are not allowed to assign documents to other users."
                });
            }

            //  VALIDATION 2:
            // Manager يستطيع أن يرفع لصالح مستخدم آخر
            if (role == "Manager" && !string.IsNullOrEmpty(dto.TargetUserId)) 
            {
                ownerId = dto.TargetUserId;
            }

            //يسلّم كل شي للـ DocumentService 
            var result = await _documentService.AddDocumentAsync(ownerId, dto); 

            await _httpClient.PostAsJsonAsync(
              "https://localhost:7141/api/ocr/process",
                   new
                     {
                    documentId = result.Document.Id,
                    filePath = Path.Combine(_env.ContentRootPath, result.Document.FilePath),
                    callbackUrl = $"https://localhost:44302/api/ocr/callback?documentId={result.Document.Id}",
                    department = result.Document.Department
                       }
                        );



            if (result.IsDuplicate)
            {
                // 409 = Conflict (التكرار)
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
        [Authorize]
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

        [Authorize]
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


        [Authorize]
        [HttpPost("search")]
        public async Task<IActionResult> SearchDocuments(SearchDocumentsDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var result = await _searchService.SearchDocumentsAsync(dto, userId, role);

            return Ok(result);
        }
        [Authorize(Roles = "User,Manager")]
        [HttpDelete("{documentId}")]
        public async Task<IActionResult> DeleteDocument(string documentId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var deleted = await _documentService.DeleteDocumentAsync(
                documentId,
                userId,
                role
            );

            if (!deleted)
                return NotFound(new { message = "Document not found or access denied" });

            return Ok(new { message = "Document deleted successfully" });
        }

        [HttpGet("{id}/view")]
        [Authorize]
        public async Task<IActionResult> View(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;
            var department = User.FindFirst("department")?.Value;

            var doc = await _documentService.ViewDocumentAsync(id, userId, role, department);

            if (doc == null)
                return Forbid();

            return Ok(doc);
        }
        [Authorize]
        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;
            var dept = User.FindFirst("department")?.Value;

            var result = await _documentService.DownloadDocumentAsync(id, userId, role, dept);

            if (result == null)
                return Forbid(); 

            return File(
                result.Value.FileStream,
                result.Value.ContentType,
                result.Value.FileName
            );
        }
        [Authorize]
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


        [Authorize(Roles = "User,Manager")]
        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateDocument(string id, [FromForm] UpdateDocumentDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
            var role = User.FindFirst(ClaimTypes.Role)?.Value!;

            var result = await _documentService.UpdateDocumentAsync(id, dto, userId, role);

            if (!result.Success)
            {
                if (result.IsDuplicate)
                {
                    return Conflict(new
                    {
                        message = "Duplicate file detected",
                        existingDocumentId = result.ExistingDocumentId
                    });
                }

                return BadRequest(new { message = result.Message });
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

    }
}

