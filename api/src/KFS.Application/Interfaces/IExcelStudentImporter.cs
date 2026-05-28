using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record ExcelStudentRow(
    int RowNumber,
    string? Email,
    string? FirstName,
    string? LastName,
    string? DateOfBirthRaw,
    string? GradeOrClass);

// One roster row, post-parse. New shape matches the school export:
// StudentId · FirstName · LastName · PreferredName · Email · Gender · Grade · Group
public record ParsedStudentRow(
    int RowNumber,
    string Email,
    string FirstName,
    string LastName,
    string? StudentNumber,
    string? PreferredName,
    string? Gender,
    string? GradeOrClass,
    ZoneGroup? AssignedGroup,
    // Kept for back-compat with the legacy roster format; null for new imports.
    DateTime? DateOfBirth);

public record StudentRowError(int RowNumber, string Field, string Message);

public record ExcelParseResult(
    IReadOnlyList<ParsedStudentRow> Valid,
    IReadOnlyList<StudentRowError> Errors);

public interface IExcelStudentImporter
{
    ExcelParseResult Parse(Stream xlsxStream);
}
