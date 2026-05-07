namespace KFS.Application.Interfaces;

public interface IBlobStorage
{
    /// <summary>Uploads bytes and returns a publicly addressable URL.</summary>
    Task<string> SaveAsync(string relativePath, byte[] content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Returns a fresh, currently-valid read URL for an already-stored blob. Necessary because
    /// SaveAsync returns a SAS-signed URL that expires (per spec, 5 min) — clients re-fetching a
    /// ticket later need a re-signed URL. Accepts either a relative path or a previously-emitted
    /// full URL (with or without SAS query string).
    /// </summary>
    string RefreshReadUrl(string storedUrlOrPath);
}
