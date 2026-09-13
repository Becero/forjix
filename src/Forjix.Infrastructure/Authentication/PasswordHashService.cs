using Forjix.Application.Abstractions.Authentication;
using Forjix.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Forjix.Infrastructure.Authentication;

internal sealed class PasswordHashService(IPasswordHasher<User> passwordHasher) : IPasswordHashService
{
    public string HashPassword(User user, string password) => passwordHasher.HashPassword(user, password);

    public bool VerifyPassword(User user, string passwordHash, string providedPassword) =>
        passwordHasher.VerifyHashedPassword(user, passwordHash, providedPassword) is not PasswordVerificationResult.Failed;
}
