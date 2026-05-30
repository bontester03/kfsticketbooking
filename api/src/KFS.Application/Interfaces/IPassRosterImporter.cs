namespace KFS.Application.Interfaces;

/// <summary>One row from a staff / photographer / personal-assistant / visitor roster.
/// Three columns: Full Name | Email | Type. The Type cell MUST match the pass type
/// the admin picked in the UI — the service rejects any row whose Type doesn't match,
/// so a mistakenly-mixed XLSX never silently issues wrong-category QRs.</summary>
public record ParsedPassRosterRow(int RowNumber, string FullName, string Email, string Type);

public record PassRosterRowError(int RowNumber, string Field, string Message);

public record PassRosterParseResult(
    IReadOnlyList<ParsedPassRosterRow> Valid,
    IReadOnlyList<PassRosterRowError> Errors);

public interface IPassRosterImporter
{
    PassRosterParseResult Parse(Stream xlsxStream);
}
