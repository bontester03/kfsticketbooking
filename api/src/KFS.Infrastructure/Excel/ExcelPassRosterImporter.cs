using ClosedXML.Excel;
using KFS.Application.Interfaces;

namespace KFS.Infrastructure.Excel;

/// <summary>Two-column roster: Full Name | Email. Row 1 is the header and is skipped.
/// Trimmed and email-shape-checked; broader validation happens at the service layer
/// (dedup against existing passes, quota check).</summary>
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

            if (string.IsNullOrEmpty(name))  { errors.Add(new(rn, "FullName", "Required")); continue; }
            if (string.IsNullOrEmpty(email)) { errors.Add(new(rn, "Email",    "Required")); continue; }
            if (!email.Contains('@'))         { errors.Add(new(rn, "Email",   "Malformed email")); continue; }

            valid.Add(new ParsedPassRosterRow(rn, name, email));
        }

        return new PassRosterParseResult(valid, errors);
    }
}
