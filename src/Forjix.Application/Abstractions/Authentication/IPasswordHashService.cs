using Forjix.Domain.Entities.Identity;

namespace Forjix.Application.Abstractions.Authentication;

public interface IPasswordHashService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string passwordHash, string providedPassword);
}
