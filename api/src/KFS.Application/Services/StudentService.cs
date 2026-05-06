using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Students;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class StudentService : IStudentService
{
    private readonly IApplicationDbContext _db;
    private readonly IExcelStudentImporter _importer;
    private readonly IPasswordHasher _hasher;

    public StudentService(IApplicationDbContext db, IExcelStudentImporter importer, IPasswordHasher hasher)
    {
        _db = db; _importer = importer; _hasher = hasher;
    }

    public async Task<StudentImportResultDto> ImportAsync(Stream xlsxStream, CancellationToken ct = default)
    {
        var parsed = _importer.Parse(xlsxStream);

        var rowResults = new List<StudentImportRowResultDto>();
        rowResults.AddRange(parsed.Errors.Select(e =>
            new StudentImportRowResultDto(e.RowNumber, false, $"{e.Field}: {e.Message}")));

        var existing = await _db.Students.Select(s => s.Email).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Student>();
        foreach (var row in parsed.Valid)
        {
            if (!row.Email.EndsWith("@stu.kfs.sch.sa", StringComparison.OrdinalIgnoreCase))
            {
                rowResults.Add(new StudentImportRowResultDto(row.RowNumber, false, "Email must end with @stu.kfs.sch.sa"));
                continue;
            }
            if (existingSet.Contains(row.Email))
            {
                rowResults.Add(new StudentImportRowResultDto(row.RowNumber, false, "Email already exists — skipped."));
                continue;
            }

            var initialPassword = ComputeInitialPassword(row.FirstName, row.DateOfBirth);
            toAdd.Add(new Student
            {
                Email = row.Email.ToLowerInvariant(),
                FirstName = row.FirstName,
                LastName = row.LastName,
                DateOfBirth = row.DateOfBirth,
                GradeOrClass = row.GradeOrClass,
                PasswordHash = _hasher.Hash(initialPassword),
                MustChangePassword = true,
                IsActive = true
            });
            existingSet.Add(row.Email);
            rowResults.Add(new StudentImportRowResultDto(row.RowNumber, true, null));
        }

        if (toAdd.Count > 0)
        {
            _db.Students.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
        }

        var imported = rowResults.Count(r => r.Imported);
        var failed = parsed.Errors.Count;
        var skipped = rowResults.Count - imported - failed;
        return new StudentImportResultDto(parsed.Valid.Count + parsed.Errors.Count, imported, skipped, failed,
            rowResults.OrderBy(r => r.RowNumber).ToList());
    }

    public async Task<IReadOnlyList<StudentDto>> ListAsync(string? search, string? status, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.Students.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x => x.Email.ToLower().Contains(s) || x.FirstName.ToLower().Contains(s) || x.LastName.ToLower().Contains(s));
        }
        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.IsActive);
        if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => !x.IsActive);

        var students = await query.OrderBy(x => x.LastName).Skip(skip).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        var ids = students.Select(s => s.Id).ToList();
        var bookings = await _db.Bookings
            .Where(b => ids.Contains(b.StudentId))
            .GroupBy(b => b.StudentId)
            .Select(g => new { StudentId = g.Key, Status = g.OrderByDescending(b => b.CreatedAt).First().Status })
            .ToListAsync(ct);
        var bookingByStudent = bookings.ToDictionary(b => b.StudentId, b => b.Status.ToString());

        return students.Select(s => new StudentDto(
            s.Id, s.Email, s.FirstName, s.LastName, s.DateOfBirth, s.GradeOrClass,
            s.IsActive, s.MustChangePassword,
            bookingByStudent.TryGetValue(s.Id, out var st) ? st : null, s.CreatedAt)).ToList();
    }

    public async Task<StudentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Students.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Student", id);
        var booking = await _db.Bookings.Where(b => b.StudentId == id)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => (BookingStatus?)b.Status).FirstOrDefaultAsync(ct);
        return new StudentDto(s.Id, s.Email, s.FirstName, s.LastName, s.DateOfBirth, s.GradeOrClass,
            s.IsActive, s.MustChangePassword, booking?.ToString(), s.CreatedAt);
    }

    public async Task<StudentDto> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default)
    {
        var s = await _db.Students.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Student", id);
        if (request.IsActive.HasValue) s.IsActive = request.IsActive.Value;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ResetPasswordResponseDto> ResetPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Students.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Student", id);
        var pwd = ComputeInitialPassword(s.FirstName, s.DateOfBirth);
        s.PasswordHash = _hasher.Hash(pwd);
        s.MustChangePassword = true;
        await _db.SaveChangesAsync(ct);
        return new ResetPasswordResponseDto(pwd);
    }

    public static string ComputeInitialPassword(string firstName, DateTime dob)
    {
        var trimmed = (firstName ?? string.Empty).Trim();
        var prefix = trimmed.Length >= 3 ? trimmed[..3] : trimmed.PadRight(3, 'X');
        // Capitalize the leading character so we always have at least one upper-case letter.
        prefix = char.ToUpperInvariant(prefix[0]) + prefix[1..].ToLowerInvariant();
        return $"{prefix}{dob:ddMMyyyy}";
    }
}
