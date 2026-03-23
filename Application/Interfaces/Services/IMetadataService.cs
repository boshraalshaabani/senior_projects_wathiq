using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Services
{


    public interface IMetadataService
    {

        // إضافة Metadata جديدة لوثيقة
        Task<bool> AddMetadataAsync(
            string documentId,
            AddMetadataDto dto,
            string userId,
            string role
            
        );

        // عرض Metadata الخاصة بوثيقة
        Task<Metadata?> ViewMetadataAsync(
            string documentId,
            string userId,
            string role
            
        );

        // تعديل Metadata < موجودة < أو إنشاؤها إن لم تكن موجودة
        Task<bool> UpdateMetadataAsync(
            string documentId,
            AddMetadataDto dto,
            string userId,
            string role
            
        );
    }
}