using ClosedXML.Excel;
using KFS.Application.Interfaces;

namespace KFS.Infrastructure.Excel;

/// <summary>Three-column roster: Full Name | Email | Type. Row 1 is the header and
/// is skipped. Cell trimming + email-shape check happen here; downstream validation
/// (Type matches the pass type the admin picked, dedup vs existing, quota) happens
/// at the service layer.</summary>
public class ExcelPassRosterImporter : IPassRosterImporter
{
    public PassRosterParseResult Parse(Stream xlsxStream)
    {
        using var wb = new XLWorkbook(xlsxStream);
        var ws = wb.Worksheets.First();
        var rows = ws.RowsUsed().Skip(1).ToList();

        var valid = new List<ParsedPassRosterRow>();
        var errors = new List<PassRosterRowError>();

        foreach (var row in rows)
        {
            var rn = row.RowNumber();
            var name  = row.Cell(1).GetString().Trim();
            var email = row.Cell(2).GetString().Trim();
            var type  = row.Cell(3).GetString().Trim();

            if (string.IsNullOrEmpty(name))  { errors.Add(new(rn, "FullName", "Required")); continue; }
            if (string.IsNullOrEmpty(email)) { errors.Add(new(rn, "Email",    "Required")); continue; }
            if (!email.Contains('@'))         { errors.Add(new(rn, "Email",   "Malformed email")); continue; }
            if (string.IsNullOrEmpty(type))   { errors.Add(new(rn, "Type",    "Required — must match the roster type picker (e.g. Staff, Photographer).")); continue; }

            valid.Add(new ParsedPassRosterRow(rn, name, email, type));
        }

        return new PassRosterParseResult(valid, errors);
    }
}
