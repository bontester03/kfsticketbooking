using ClosedXML.Excel;
using KFS.Application.Interfaces;

namespace KFS.Infrastructure.Excel;

public class ExcelStudentImporter : IExcelStudentImporter
{
    public ExcelParseResult Parse(Stream xlsxStream)
    {
        using var wb = new XLWorkbook(xlsxStream);
        var ws = wb.Worksheets.First();
        var rows = ws.RowsUsed().Skip(1).ToList(); // assume header row

        var valid = new List<ParsedStudentRow>();
        var errors = new List<StudentRowError>();

        foreach (var row in rows)
        {
            var rn = row.RowNumber();
            var email = row.Cell(1).GetString().Trim();
            var first = row.Cell(2).GetString().Trim();
            var last = row.Cell(3).GetString().Trim();
            var dobRaw = row.Cell(4).GetString().Trim();
            var grade = row.Cell(5).GetString().Trim();

            if (string.IsNullOrEmpty(email)) { errors.Add(new(rn, "Email", "Required")); continue; }
            if (string.IsNullOrEmpty(first)) { errors.Add(new(rn, "FirstName", "Required")); continue; }
            if (string.IsNullOrEmpty(last))  { errors.Add(new(rn, "LastName",  "Required")); continue; }
            if (string.IsNullOrEmpty(dobRaw)) { errors.Add(new(rn, "DateOfBirth", "Required")); continue; }
            if (!email.Contains('@')) { errors.Add(new(rn, "Email", "Malformed email")); continue; }

            if (!TryParseDob(dobRaw, out var dob))
            {
                errors.Add(new(rn, "DateOfBirth", "Use DD-MM-YYYY (e.g. 15-03-2010)"));
                continue;
            }

            valid.Add(new ParsedStudentRow(rn, email, first, last, dob,
                string.IsNullOrEmpty(grade) ? null : grade));
        }

        return new ExcelParseResult(valid, errors);
    }

    private static bool TryParseDob(string raw, out DateTime dob)
    {
        // Accept DD-MM-YYYY, DD/MM/YYYY, or Excel-numeric (already coerced to string by GetString()).
        var formats = new[] { "dd-MM-yyyy", "dd/MM/yyyy", "d-M-yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(raw, formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out dob))
            return true;

        if (double.TryParse(raw, out var oa))
        {
            try { dob = DateTime.FromOADate(oa).Date; return true; }
            catch { }
        }

        dob = default;
        return false;
    }
}
