using ClosedXML.Excel;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;

namespace KFS.Infrastructure.Excel;

public class ExcelStudentImporter : IExcelStudentImporter
{
    // School roster columns (post-2026 layout):
    //   1: Student ID         2: First Name      3: Last Name        4: Preferred Name
    //   5: Email              6: Gender          7: Grade            8: Group
    //
    // Gender accepts Male/Female, Boys/Girls, or 1/2. Group accepts VIP A/VIP B,
    // A/B, or 1/2. Final event validation happens in StudentService.
    // First row is a header and is skipped.
    public ExcelParseResult Parse(Stream xlsxStream)
    {
        using var wb = new XLWorkbook(xlsxStream);
        var ws = wb.Worksheets.First();
        var rows = ws.RowsUsed().Skip(1).ToList();

        var valid = new List<ParsedStudentRow>();
        var errors = new List<StudentRowError>();

        foreach (var row in rows)
        {
            var rn = row.RowNumber();
            var studentId = row.Cell(1).GetString().Trim();
            var first     = row.Cell(2).GetString().Trim();
            var last      = row.Cell(3).GetString().Trim();
            var preferred = row.Cell(4).GetString().Trim();
            var email     = row.Cell(5).GetString().Trim();
            var gender    = row.Cell(6).GetString().Trim();
            var grade     = row.Cell(7).GetString().Trim();
            var groupRaw  = row.Cell(8).GetString().Trim();

            if (string.IsNullOrEmpty(email)) { errors.Add(new(rn, "Email", "Required")); continue; }
            if (!email.Contains('@'))         { errors.Add(new(rn, "Email", "Malformed email")); continue; }
            if (string.IsNullOrEmpty(first)) { errors.Add(new(rn, "FirstName", "Required")); continue; }
            if (string.IsNullOrEmpty(last))  { errors.Add(new(rn, "LastName", "Required")); continue; }

            ZoneGroup? group = null;
            if (!string.IsNullOrEmpty(groupRaw))
            {
                var g = groupRaw.ToUpperInvariant().Replace(" ", "").Replace("-", "");
                if (g is "VIPA" or "A" or "1" or "GROUPA" or "VIP1")      group = ZoneGroup.A;
                else if (g is "VIPB" or "B" or "2" or "GROUPB" or "VIP2") group = ZoneGroup.B;
                else { errors.Add(new(rn, "Group", "Must be 'VIP A'/'VIP B' or 1/2.")); continue; }
            }

            valid.Add(new ParsedStudentRow(
                RowNumber: rn,
                Email: email,
                FirstName: first,
                LastName: last,
                StudentNumber: string.IsNullOrEmpty(studentId) ? null : studentId,
                PreferredName: string.IsNullOrEmpty(preferred) ? null : preferred,
                Gender: string.IsNullOrEmpty(gender) ? null : gender,
                GradeOrClass: string.IsNullOrEmpty(grade) ? null : grade,
                AssignedGroup: group,
                DateOfBirth: null));
        }

        return new ExcelParseResult(valid, errors);
    }
}
