using System.Security.Cryptography;
using SnowOps.Api.Domain;
using SnowOps.Api.Domain.Entities;

namespace SnowOps.Api.Services;

public sealed class PhotoService(IConfiguration config)
{
    private readonly string _basePath = config["Photos:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "photos");

    public async Task<DefectPhoto> SaveAsync(IFormFile file, Guid defectId, PhotoKind kind, Guid uploadedByUserId, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var objectKey = $"{defectId}/{kind.ToString().ToLower()}/{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(_basePath, objectKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        byte[] sha256Bytes;
        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        using (var sha256 = SHA256.Create())
        await using (var cs = new CryptoStream(fs, sha256, CryptoStreamMode.Write))
        {
            await file.CopyToAsync(cs, ct);
            await cs.FlushFinalBlockAsync(ct);
            sha256Bytes = sha256.Hash!;
        }

        return new DefectPhoto
        {
            Id = Guid.NewGuid(),
            DefectId = defectId,
            Kind = kind,
            StorageProvider = "local",
            ObjectKey = objectKey,
            Url = $"/photos/{objectKey}",
            ContentType = file.ContentType ?? "image/jpeg",
            SizeBytes = file.Length,
            Sha256 = sha256Bytes,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }
}
