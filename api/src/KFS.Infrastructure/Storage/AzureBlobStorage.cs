using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using KFS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KFS.Infrastructure.Storage;

/// Azure Blob Storage implementation. Two ways to authenticate:
/// 1. Production (Azure App Service) — Managed Identity via DefaultAzureCredential.
/// 2. Local dev (Azurite) — connection string via Storage:ConnectionString.
public class AzureBlobStorage : IBlobStorage
{
    private readonly BlobServiceClient _client;
    private readonly string _container;
    private readonly bool _emitSasUrls;
    private readonly TimeSpan _sasLifetime;
    /// When set, replaces the scheme+host+port of returned blob URLs. Needed in dev because
    /// the BlobServiceClient is wired to the Docker-internal `http://azurite:10000` hostname
    /// but the browser running on the host can only reach `http://localhost:10000`.
    private readonly string? _publicBaseUrl;
    private readonly ILogger<AzureBlobStorage> _log;

    public AzureBlobStorage(IConfiguration config, ILogger<AzureBlobStorage> log)
    {
        _log = log;
        _container = config.GetValue<string>("Storage:Container") ?? "qr-codes";
        _sasLifetime = TimeSpan.FromMinutes(config.GetValue<int?>("Storage:SasLifetimeMinutes") ?? 5);
        _publicBaseUrl = config.GetValue<string>("Storage:PublicBaseUrl");

        var connectionString = config.GetValue<string>("Storage:ConnectionString");
        var serviceUri = config.GetValue<string>("Storage:ServiceUri");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _client = new BlobServiceClient(connectionString);
            // Connection-string mode (Azurite / dev) ships an account key, so the BlobClient
            // can mint a SAS — the only way to read a Private container without an auth header.
            _emitSasUrls = true;
        }
        else if (!string.IsNullOrWhiteSpace(serviceUri))
        {
            // Production path: App Service Managed Identity → Storage account.
            _client = new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential());
            _emitSasUrls = true;
        }
        else
        {
            throw new InvalidOperationException(
                "Storage:Provider=AzureBlob requires either Storage:ConnectionString (Azurite/dev) " +
                "or Storage:ServiceUri (Azure with Managed Identity).");
        }
    }

    public async Task<string> SaveAsync(string relativePath, byte[] content, string contentType, CancellationToken ct = default)
    {
        var safe = relativePath.Replace("..", "").TrimStart('/');
        var (container, blobName) = SplitContainerAndBlob(safe);

        var containerClient = _client.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = containerClient.GetBlobClient(blobName);
        using var ms = new MemoryStream(content);
        await blob.UploadAsync(ms, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        if (!_emitSasUrls) return Public(blob.Uri);

        if (!blob.CanGenerateSasUri)
        {
            _log.LogWarning("BlobClient cannot generate user-delegation SAS — returning canonical URL. " +
                            "On Azure with Managed Identity this is expected; clients fetch via the API's SAS endpoint.");
            return Public(blob.Uri);
        }

        // 5-minute SAS by default; long enough for an email client to fetch the inline image.
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(_sasLifetime));
        return Public(sasUri);
    }

    public string RefreshReadUrl(string storedUrlOrPath)
    {
        if (string.IsNullOrWhiteSpace(storedUrlOrPath)) return storedUrlOrPath;
        var relativePath = ExtractBlobPath(storedUrlOrPath);
        var (container, blobName) = SplitContainerAndBlob(relativePath);
        var blob = _client.GetBlobContainerClient(container).GetBlobClient(blobName);

        if (_emitSasUrls && blob.CanGenerateSasUri)
        {
            var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(_sasLifetime));
            return Public(sasUri);
        }
        return Public(blob.Uri);
    }

    private string Public(Uri internalUri)
    {
        if (string.IsNullOrEmpty(_publicBaseUrl)) return internalUri.ToString();
        return $"{_publicBaseUrl.TrimEnd('/')}{internalUri.PathAndQuery}";
    }

    /// Layout: top-level path segment is the container; rest is the blob key.
    /// `qr-codes/{eventId}/{ticket}.png` → container=`qr-codes`, blob=`{eventId}/{ticket}.png`.
    /// Falls back to the configured default container if only a single segment.
    private (string container, string blob) SplitContainerAndBlob(string path)
    {
        var firstSlash = path.IndexOf('/');
        if (firstSlash < 0) return (_container, path);
        return (path[..firstSlash], path[(firstSlash + 1)..]);
    }

    /// Accepts a relative path (`qr-codes/.../foo.png`) or a previously-emitted full URL
    /// (`http://host/devstoreaccount1/qr-codes/.../foo.png?sas...`). Returns the blob path.
    private string ExtractBlobPath(string input)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(input);
                // Drop leading slash and the storage-account segment that Azurite always prefixes.
                var path = uri.AbsolutePath.TrimStart('/');
                const string accountPrefix = "devstoreaccount1/";
                if (path.StartsWith(accountPrefix, StringComparison.OrdinalIgnoreCase))
                    path = path[accountPrefix.Length..];
                return path;
            }
            catch
            {
                return input.TrimStart('/');
            }
        }
        return input.TrimStart('/');
    }
}
