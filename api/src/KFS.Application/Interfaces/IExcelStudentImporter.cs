namespace KFS.Application.Interfaces;

public record ExcelStudentRow(
    int RowNumber,
    string? Email,
    string? FirstName,
    string? LastName,
    string? DateOfBirthRaw,
    string? GradeOrClass);

public record ParsedStudentRow(
    int RowNumber,
    string Email,
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string? GradeOrClass);

public record StudentRowError(int RowNumber, string Field, string Message);

public record ExcelParseResult(
    IReadOnlyList<ParsedStudentRow> Valid,
    IReadOnlyList<StudentRowError> Errors);

public interface IExcelStudentImporter
{
    ExcelParseResult Parse(Stream xlsxStream);
}
