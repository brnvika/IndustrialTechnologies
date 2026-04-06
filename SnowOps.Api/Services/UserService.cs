using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SnowOps.Api.Data;
using SnowOps.Api.Domain.Entities;

namespace SnowOps.Api.Services;

public sealed class UserService(AppDbContext db)
{
    public async Task<AppUser?> GetByLoginAsync(string login, CancellationToken ct = default)
    {
        var normalizedLogin = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(normalizedLogin))
            return null;

        return await db.Users.FirstOrDefaultAsync(u => u.Login == normalizedLogin, ct);
    }

    public async Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
    }

    public async Task<AppUser?> RegisterAsync(string login, string password, CancellationToken ct = default)
    {
        var normalizedLogin = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(normalizedLogin) || string.IsNullOrWhiteSpace(password))
            return null;

        if (await db.Users.AnyAsync(u => u.Login == normalizedLogin, ct))
            return null;

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Login = normalizedLogin,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<AppUser?> AuthenticateAsync(string login, string password, CancellationToken ct = default)
    {
        var normalizedLogin = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(normalizedLogin) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == normalizedLogin, ct);
        if (user is null)
            return null;

        return VerifyPassword(password, user.PasswordHash) ? user : null;
    }

    private static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();

    private static string HashPassword(string password)
    {
        const int iterations = 100_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = derive.GetBytes(32);

        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
            return false;

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var actualHash = derive.GetBytes(expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
