using Forjix.Domain.Entities.Identity;

namespace Forjix.Domain.Tests;

public sealed class UserRoleModelTests
{
    [Fact]
    public void UserCanBelongToMoreThanOneRole()
    {
        var user = new User
        {
            Name = "Test user",
            Email = "user@example.test",
            NormalizedEmail = "USER@EXAMPLE.TEST",
            PasswordHash = "not-a-real-password-hash"
        };
        var firstRole = new Role { Name = "First", NormalizedName = "FIRST" };
        var secondRole = new Role { Name = "Second", NormalizedName = "SECOND" };

        user.UserRoles.Add(new UserRole { User = user, Role = firstRole });
        user.UserRoles.Add(new UserRole { User = user, Role = secondRole });

        Assert.Equal(2, user.UserRoles.Count);
    }
}

