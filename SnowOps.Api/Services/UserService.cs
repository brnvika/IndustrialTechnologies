using Microsoft.EntityFrameworkCore;
using SnowOps.Api.Data;
using SnowOps.Api.Domain.Entities;

namespace SnowOps.Api.Services;

public sealed class UserService(AppDbContext db)
{
    public async Task<AppUser> GetOrCreateAsync(string login, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login, ct);
        if (user is not null) return user;

        user = new AppUser
        {
            Id = Guid.NewGuid(),
            Login = login,
            DisplayName = login,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }
}
