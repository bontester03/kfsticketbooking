namespace KFS.Application.Interfaces;

/// <summary>One row from a staff / photographer / personal-assistant / visitor roster.
/// Two columns: Full Name, Email. Used by AdminPassService.GenerateFromRosterAsync to
/// produce one QR per row and email each holder their pass.</summary>
public record ParsedPassRosterRow(int RowNumber, string FullName, string Email);

public record PassRosterRowError(int RowNumber, string Field, string Message);

public record PassRosterParseResult(
    IReadOnlyList<ParsedPassRosterRow> Valid,
    IReadOnlyList<PassRosterRowError> Errors);

public interface IPassRosterImporter
{
    PassRosterParseResult Parse(Stream xlsxStream);
}
