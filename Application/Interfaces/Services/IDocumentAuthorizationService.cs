using eArchiveSystem.Application.DTOs;
using eArchiveSystem.Domain.Models;

namespace eArchiveSystem.Application.Interfaces.Services
{
    public interface IDocumentAuthorizationService
    {
        bool CanAddForOwner(User actor, User owner);
        bool CanView(User actor, Document document);
        bool CanEdit(User actor, Document document);
        bool CanDelete(User actor, Document document);
        SearchAccessScope BuildSearchScope(User actor);
    }
}
