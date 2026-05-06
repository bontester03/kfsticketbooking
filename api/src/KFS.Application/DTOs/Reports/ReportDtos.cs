using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Reports;

public record GroupReportRow(
    string RowLabel,
    int SeatNumber,
    ZoneSide Side,
    string ParentName,
    string LinkedStudent,
    string Email,
    DateTime BookedAt);

public record GroupReportData(
    ZoneGroup Group,
    string EventName,
    DateTime EventDate,
    IReadOnlyList<GroupReportRow> Rows);

public record DashboardStatsDto(
    int StudentsTotal,
    int StudentsLoggedIn,
    int CartCount,
    int Confirmed,
    int Cancelled,
    int ScansToday,
    IReadOnlyList<ZoneCapacityDto> Zones);

public record ZoneCapacityDto(string Zone, int Capacity, int Issued, double PercentIssued);
