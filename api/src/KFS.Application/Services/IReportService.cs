using KFS.Application.DTOs.Reports;
using KFS.Domain.Enums;

namespace KFS.Application.Services;

public interface IReportService
{
    Task<GroupReportData> GetGroupReportAsync(Guid eventId, ZoneGroup group, CancellationToken ct = default);
    Task<byte[]> ExportXlsxAsync(GroupReportData data, CancellationToken ct = default);
    Task<byte[]> ExportPdfAsync(GroupReportData data, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(GroupReportData data, CancellationToken ct = default);
    Task<DashboardStatsDto> GetDashboardAsync(Guid eventId, CancellationToken ct = default);
}
