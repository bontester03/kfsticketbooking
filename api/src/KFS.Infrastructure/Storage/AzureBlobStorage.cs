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
    private readonly ILogger<AzureBlobStorage> _log;

    public AzureBlobStorage(IConfiguration config, ILogger<AzureBlobStorage> log)
    {
        _log = log;
        _container = config.GetValue<string>("Storage:Container") ?? "qr-codes";
        _sasLifetime = TimeSpan.FromMinutes(config.GetValue<int?>("Storage:SasLifetimeMinutes") ?? 5);

        var connectionString = config.GetValue<string>("Storage:ConnectionString");
        var serviceUri = config.GetValue<string>("Storage:ServiceUri");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _client = new BlobServiceClient(connectionString);
            // Azurite + connection-string mode: SAS works but plain URLs are fine for local dev.
            _emitSasUrls = false;
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

        if (!_emitSasUrls) return blob.Uri.ToString();

        if (!blob.CanGenerateSasUri)
        {
            _log.LogWarning("BlobClient cannot generate user-delegation SAS — returning canonical URL. " +
                            "On Azure with Managed Identity this is expected; clients fetch via the API's SAS endpoint.");
            return blob.Uri.ToString();
        }

        // 5-minute SAS by default; long enough for an email client to fetch the inline image.
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(_sasLifetime));
        return sasUri.ToString();
    }

    /// Layout: top-level path segment is the container; rest is the blob key.
    /// `qr/{eventId}/{ticket}.png` → container=`qr`, blob=`{eventId}/{ticket}.png`.
    /// Falls back to the configured default container if only a single segment.
    private (string container, string blob) SplitContainerAndBlob(string path)
    {
        var firstSlash = path.IndexOf('/');
        if (firstSlash < 0) return (_container, path);
        return (path[..firstSlash], path[(firstSlash + 1)..]);
    }
}
