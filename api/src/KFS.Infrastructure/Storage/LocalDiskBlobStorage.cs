using KFS.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace KFS.Infrastructure.Storage;

public class LocalDiskBlobStorage : IBlobStorage
{
    private readonly string _root;
    private readonly string _publicBase;

    public LocalDiskBlobStorage(IConfiguration config)
    {
        _root = config.GetValue<string>("Storage:LocalRoot") ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        _publicBase = (config.GetValue<string>("Storage:PublicBase") ?? "/static").TrimEnd('/');
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string relativePath, byte[] content, string contentType, CancellationToken ct = default)
    {
        var safe = relativePath.Replace("..", "").TrimStart('/');
        var full = Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(full, content, ct);
        return $"{_publicBase}/{safe}";
    }
}
