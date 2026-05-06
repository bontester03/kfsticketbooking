namespace KFS.Application.Interfaces;

public interface IBlobStorage
{
    /// <summary>Uploads bytes and returns a publicly addressable URL.</summary>
    Task<string> SaveAsync(string relativePath, byte[] content, string contentType, CancellationToken ct = default);
}
