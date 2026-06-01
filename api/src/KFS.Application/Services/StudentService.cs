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
    private readonly IEmailService _email;

    public StudentService(IApplicationDbContext db, IExcelStudentImporter importer, IPasswordHasher hasher, IEmailService email)
    {
        _db = db; _importer = importer; _hasher = hasher; _email = email;
    }

    public async Task<StudentImportResultDto> ImportAsync(Guid eventId, Stream xlsxStream, CancellationToken ct = default)
    {
        var targetEvent = await _db.Events.FindAsync(new object[] { eventId }, ct)
            ?? throw new NotFoundException("Event", eventId);

        // The Gender string we accept depends on which event the admin is uploading to.
        // Boys event accepts only "Male" rows; Girls event accepts only "Female" rows.
        // Anything else is rejected per row with a clear message so the admin sees exactly
        // which rows were wrong instead of a silent partial import.
        var expectedGender = targetEvent.Gender;
        var expectedGenderText = GenderText(expectedGender);
        var otherGenderText = expectedGender == EventGender.Male ? "Female" : "Male";

        var parsed = _importer.Parse(xlsxStream);

        var rowResults = new List<StudentImportRowResultDto>();
        rowResults.AddRange(parsed.Errors.Select(e =>
            new StudentImportRowResultDto(e.RowNumber, false, $"{e.Field}: {e.Message}")));

        var existing = await _db.Students.Select(s => s.Email).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Local helper — every row-result for a parsed row echoes the row's email + name
        // back so the UI can show "Row 4 — alex.smith@gmail.com (Alex Smith): wrong domain"
        // instead of just a row number.
        StudentImportRowResultDto Result(KFS.Application.Interfaces.ParsedStudentRow row, bool ok, string? msg) =>
            new(row.RowNumber, ok, msg, row.Email, row.FirstName, row.LastName);

        var toAdd = new List<Student>();
        foreach (var row in parsed.Valid)
        {
            // Email-domain whitelist removed at client request (2026-05-31).
            // Any valid-shape email is now accepted; the importer's parser already
            // rejects malformed addresses earlier.
            if (existingSet.Contains(row.Email))
            {
                rowResults.Add(Result(row, false, "Email already exists — skipped."));
                continue;
            }

            var parsedGender = ParseGender(row.Gender);
            if (string.IsNullOrWhiteSpace(row.Gender))
            {
                rowResults.Add(Result(row, false, "Gender is required."));
                continue;
            }
            if (parsedGender.HasValue && parsedGender.Value != expectedGender)
            {
                rowResults.Add(Result(row, false,
                    $"Wrong event — this row is for the {otherGenderText} event. Upload it on /admin/{(expectedGender == EventGender.Male ? "girls" : "boys")}/students instead."));
                continue;
            }
            if (!parsedGender.HasValue)
            {
                rowResults.Add(Result(row, false,
                    $"Gender must be {expectedGenderText} for this event. Accepted values: Male/Female, Boys/Girls, or 1/2."));
                continue;
            }

            var initialPassword = ComputeInitialPassword(row.FirstName, row.StudentNumber, row.DateOfBirth);
            toAdd.Add(new Student
            {
                Email = row.Email.ToLowerInvariant(),
                FirstName = row.FirstName,
                LastName = row.LastName,
                StudentNumber = row.StudentNumber,
                PreferredName = row.PreferredName,
                Gender = expectedGenderText,
                EventId = targetEvent.Id,
                GradeOrClass = row.GradeOrClass,
                AssignedGroup = row.AssignedGroup,
                DateOfBirth = row.DateOfBirth,
                PasswordHash = _hasher.Hash(initialPassword),
                MustChangePassword = true,
                IsActive = true
            });
            existingSet.Add(row.Email);
            rowResults.Add(Result(row, true, null));
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

    public async Task<IReadOnlyList<StudentDto>> ListAsync(Guid eventId, string? search, string? status, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.Students.Where(s => s.EventId == eventId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x => x.Email.ToLower().Contains(s) || x.FirstName.ToLower().Contains(s) || x.LastName.ToLower().Contains(s));
        }
        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.IsActive);
        if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => !x.IsActive);

        var students = await query.OrderBy(x => x.LastName).Skip(skip).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        var ids = students.Select(s => s.Id).ToList();

        // Latest booking per student (with items + seats), so we can show status AND the seat labels.
        var allBookings = await _db.Bookings
            .Where(b => ids.Contains(b.StudentId) && b.EventId == eventId)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .ToListAsync(ct);
        var latestByStudent = allBookings
            .GroupBy(b => b.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CreatedAt).First());

        var bookingByStudent = latestByStudent.ToDictionary(kv => kv.Key, kv => kv.Value.Status.ToString());
        var seatsByStudent = latestByStudent
            .Where(kv => kv.Value.Status == BookingStatus.Confirmed)
            .ToDictionary(kv => kv.Key, kv => string.Join(" & ",
                kv.Value.Items.OrderBy(i => i.ParentRole)
                    .Select(i => i.Seat is null ? string.Empty : $"{i.Seat.RowLabel}{i.Seat.SeatNumber}")
                    .Where(label => !string.IsNullOrEmpty(label))));

        return students.Select(s => new StudentDto(
            s.Id, s.Email, s.FirstName, s.LastName, s.DateOfBirth, s.GradeOrClass,
            s.IsActive, s.MustChangePassword,
            bookingByStudent.TryGetValue(s.Id, out var st) ? st : null, s.CreatedAt,
            seatsByStudent.TryGetValue(s.Id, out var seats) && !string.IsNullOrEmpty(seats) ? seats : null,
            s.StudentNumber, s.PreferredName, s.Gender, s.AssignedGroup.HasValue ? (int?)s.AssignedGroup.Value : null)).ToList();
    }

    public async Task<StudentDto> GetAsync(Guid id, Guid? eventId = null, CancellationToken ct = default)
    {
        var s = await _db.Students.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Student", id);
        EnsureStudentEvent(s, eventId);
        var booking = await _db.Bookings.Where(b => b.StudentId == id && (!eventId.HasValue || b.EventId == eventId.Value))
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => (BookingStatus?)b.Status).FirstOrDefaultAsync(ct);
        return new StudentDto(s.Id, s.Email, s.FirstName, s.LastName, s.DateOfBirth, s.GradeOrClass,
            s.IsActive, s.MustChangePassword, booking?.ToString(), s.CreatedAt,
            null, s.StudentNumber, s.PreferredName, s.Gender,
            s.AssignedGroup.HasValue ? (int?)s.AssignedGroup.Value : null);
    }

    public async Task<StudentDto> UpdateAsync(Guid id, UpdateStudentRequest request, Guid? eventId = null, CancellationToken ct = default)
    {
        var s = await _db.Students.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Student", id);
        var scopeEventId = eventId ?? request.EventId;
        EnsureStudentEvent(s, scopeEventId);
        if (request.IsActive.HasValue) s.IsActive = request.IsActive.Value;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, scopeEventId, ct);
    }

    public async Task<ResetPasswordResponseDto> ResetPasswordAsync(Guid id, Guid? eventId = null, CancellationToken ct = default)
    {
        var s = await _db.Students.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException("Student", id);
        EnsureStudentEvent(s, eventId);
        var pwd = ComputeInitialPassword(s.FirstName, s.StudentNumber, s.DateOfBirth);
        s.PasswordHash = _hasher.Hash(pwd);
        s.MustChangePassword = true;
        await _db.SaveChangesAsync(ct);

        // Fire-and-forget the email so the admin sees the new password instantly. A slow or
        // failing SMTP server (auth disabled, proxy, etc.) won't block the response — the
        // SmtpEmailService logs its own failures.
        // Pulls the event name from the STUDENT's event (Boys vs Girls).
        var ev = await _db.Events.FindAsync(new object[] { s.EventId }, ct);
        var eventName = ev?.Name ?? "King Faisal School Event";
        var html = $@"
<div style='font-family:Arial,sans-serif;max-width:520px;margin:auto;color:#14241f'>
  <h2 style='color:#0d3128'>Your password has been reset</h2>
  <p>Hello {System.Net.WebUtility.HtmlEncode(s.FirstName)},</p>
  <p>Your password for the <strong>{System.Net.WebUtility.HtmlEncode(eventName)}</strong> booking portal has been reset by the school office.</p>
  <p style='font-size:15px'>Email: <strong>{System.Net.WebUtility.HtmlEncode(s.Email)}</strong><br/>
     Temporary password: <strong style='font-family:monospace;font-size:16px'>{System.Net.WebUtility.HtmlEncode(pwd)}</strong></p>
  <p>Please sign in and change it. If you didn't request this, contact the school office.</p>
</div>";
        var outgoing = new OutgoingEmail(s.Email, $"{eventName} — password reset", html);
        var sender = _email;
        _ = Task.Run(async () =>
        {
            try { await sender.SendAsync(outgoing, CancellationToken.None); }
            catch { /* SmtpEmailService already logs the exception */ }
        });

        return new ResetPasswordResponseDto(pwd);
    }

    public async Task DeleteAsync(Guid id, Guid? eventId = null, CancellationToken ct = default)
    {
        var deleted = await DeleteManyAsync(new[] { id }, eventId, ct);
        if (deleted == 0) throw new NotFoundException("Student", id);
    }

    public async Task<int> DeleteManyAsync(IEnumerable<Guid> ids, Guid? eventId = null, CancellationToken ct = default)
    {
        var requestedIds = ids.Distinct().ToList();
        var idSet = await _db.Students
            .Where(s => requestedIds.Contains(s.Id) && (!eventId.HasValue || s.EventId == eventId.Value))
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (idSet.Count == 0) return 0;

        // FK-safe cascade for the targeted students only. Same order as DeleteAllAsync,
        // but every step is scoped by StudentId / BookingId set.
        var bookingIds = await _db.Bookings.Where(b => idSet.Contains(b.StudentId))
                                            .Select(b => b.Id).ToListAsync(ct);
        if (bookingIds.Count > 0)
        {
            var itemIds = await _db.BookingItems.Where(bi => bookingIds.Contains(bi.BookingId))
                                                .Select(bi => bi.Id).ToListAsync(ct);
            if (itemIds.Count > 0)
            {
                var seatScans = await _db.ScanLogs
                    .Where(s => s.ScannedItemType == ScannedItemType.BookingItem && s.ItemId != null && itemIds.Contains(s.ItemId.Value))
                    .ToListAsync(ct);
                if (seatScans.Count > 0) _db.ScanLogs.RemoveRange(seatScans);
                var items = await _db.BookingItems.Where(bi => bookingIds.Contains(bi.BookingId)).ToListAsync(ct);
                _db.BookingItems.RemoveRange(items);
            }
            var bookings = await _db.Bookings.Where(b => idSet.Contains(b.StudentId)).ToListAsync(ct);
            _db.Bookings.RemoveRange(bookings);
        }

        var studentPasses = await _db.AdminPasses.Where(p => p.StudentId != null && idSet.Contains(p.StudentId.Value))
                                                  .ToListAsync(ct);
        if (studentPasses.Count > 0)
        {
            var passIds = studentPasses.Select(p => p.Id).ToList();
            var passScans = await _db.ScanLogs
                .Where(s => s.ScannedItemType == ScannedItemType.AdminPass && s.ItemId != null && passIds.Contains(s.ItemId.Value))
                .ToListAsync(ct);
            if (passScans.Count > 0) _db.ScanLogs.RemoveRange(passScans);
            _db.AdminPasses.RemoveRange(studentPasses);
        }

        var resets = await _db.PasswordResets.Where(r => idSet.Contains(r.StudentId)).ToListAsync(ct);
        if (resets.Count > 0) _db.PasswordResets.RemoveRange(resets);

        var students = await _db.Students.Where(s => idSet.Contains(s.Id)).ToListAsync(ct);
        var count = students.Count;
        _db.Students.RemoveRange(students);

        await _db.SaveChangesAsync(ct);
        return count;
    }

    public async Task<int> DeleteAllAsync(Guid eventId, CancellationToken ct = default)
    {
        var ids = await _db.Students
            .Where(s => s.EventId == eventId)
            .Select(s => s.Id)
            .ToListAsync(ct);
        return await DeleteManyAsync(ids, eventId, ct);
    }

    public async Task<SendWelcomeEmailsResponseDto> SendWelcomeEmailsAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.Events.FindAsync(new object[] { eventId }, ct);
        var eventName = ev?.Name ?? "King Faisal School Event";
        // Per-event roster — Boys-event welcome batch never emails Girls students.
        var students = await _db.Students.Where(s => s.IsActive && s.EventId == eventId).ToListAsync(ct);
        if (students.Count == 0) return new SendWelcomeEmailsResponseDto(0, 0);

        // Reset each student to their initial password so the temp password in the email actually works.
        var creds = new List<(string Email, string FirstName, string Password)>(students.Count);
        foreach (var s in students)
        {
            var pwd = ComputeInitialPassword(s.FirstName, s.StudentNumber, s.DateOfBirth);
            s.PasswordHash = _hasher.Hash(pwd);
            s.MustChangePassword = true;
            creds.Add((s.Email, s.FirstName, pwd));
        }
        await _db.SaveChangesAsync(ct);

        // Load the embedded KFS logo once — referenced inline via cid:kfslogo in every email.
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Email", "kfs-logo-full.jpg");
        var logoBytes = File.Exists(logoPath) ? await File.ReadAllBytesAsync(logoPath, ct) : null;
        var portalUrl = Environment.GetEnvironmentVariable("Email__PortalUrl") ?? "http://localhost:5173";

        // Fire-and-forget background batch so the admin gets an instant response.
        var sender = _email;
        var subject = $"{eventName} — Your booking account is ready";
        _ = Task.Run(async () =>
        {
            foreach (var c in creds)
            {
                try
                {
                    var html = BuildWelcomeHtml(eventName, c.FirstName, c.Email, c.Password, portalUrl, logoBytes != null);
                    var attachments = logoBytes is null
                        ? null
                        : new[] { new EmailAttachment("kfs-logo.jpg", "image/jpeg", logoBytes, "kfslogo") };
                    await sender.SendAsync(new OutgoingEmail(c.Email, subject, html, attachments), CancellationToken.None);
                }
                catch { /* SmtpEmailService logs per-message failures */ }
            }
        });

        return new SendWelcomeEmailsResponseDto(students.Count, students.Count);
    }

    private static void EnsureStudentEvent(Student student, Guid? eventId)
    {
        if (eventId.HasValue && student.EventId != eventId.Value)
            throw new NotFoundException("Student", student.Id);
    }

    private static EventGender? ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var g = value.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
        return g switch
        {
            "male" or "m" or "boy" or "boys" or "1" => EventGender.Male,
            "female" or "f" or "girl" or "girls" or "2" => EventGender.Female,
            _ => null
        };
    }

    private static string GenderText(EventGender gender) =>
        gender == EventGender.Male ? "Male" : "Female";

    private static string BuildWelcomeHtml(string eventName, string firstName, string email, string pwd, string portalUrl, bool includeLogo)
    {
        string E(string s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
        var logoBlock = includeLogo
            ? "<div style=\"margin-top:32px;padding-top:24px;border-top:1px solid #e5e7eb;text-align:center\">" +
              "<img src=\"cid:kfslogo\" alt=\"King Faisal School\" style=\"height:60px;width:auto;display:inline-block\" />" +
              "<div style=\"margin-top:8px;font-size:11px;color:#94a3b8;letter-spacing:0.14em;text-transform:uppercase\">King Faisal School · Cultural Society</div>" +
              "</div>"
            : string.Empty;

        return
            "<!doctype html><html><body style=\"margin:0;padding:0;background:#fbf8f0;font-family:-apple-system,Segoe UI,Arial,sans-serif;color:#14241f\">" +
            "<div style=\"max-width:560px;margin:0 auto;padding:32px 24px;background:#ffffff\">" +
              "<div style=\"text-align:center;margin-bottom:6px\">" +
                "<span style=\"display:inline-block;font-size:11px;letter-spacing:0.28em;text-transform:uppercase;color:#a08b16;font-weight:700\">KFS Booking</span>" +
              "</div>" +
              $"<h1 style=\"margin:6px 0 4px;color:#0d3128;font-weight:600;font-size:24px;text-align:center;letter-spacing:-0.01em\">{E(eventName)}</h1>" +
              "<p style=\"text-align:center;color:#4a5a55;margin:0 0 26px;font-size:14px\">Your booking account is ready.</p>" +

              $"<p style=\"font-size:15px;line-height:1.6\">Hello <strong>{E(firstName)}</strong>,</p>" +
              $"<p style=\"font-size:14.5px;line-height:1.6;color:#4a5a55\">Your account on the {E(eventName)} parent-booking portal has been created. Use the credentials below to sign in and reserve your seats.</p>" +

              "<table cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;border-collapse:separate;background:#f1ebda;border-radius:6px;margin:18px 0;font-size:14px\">" +
                "<tr><td style=\"padding:12px 18px;color:#4a5a55\">Email</td>" +
                $"<td style=\"padding:12px 18px;text-align:right;font-weight:600;color:#0d3128\">{E(email)}</td></tr>" +
                "<tr><td style=\"padding:12px 18px;color:#4a5a55;border-top:1px solid #e6dec3\">Temporary password</td>" +
                $"<td style=\"padding:12px 18px;text-align:right;font-family:'Courier New',monospace;font-weight:700;color:#0d3128;border-top:1px solid #e6dec3\">{E(pwd)}</td></tr>" +
              "</table>" +

              "<p style=\"font-size:14.5px;color:#0d3128;margin:18px 0 6px;font-weight:600\">What to do next</p>" +
              "<ol style=\"font-size:14px;color:#4a5a55;line-height:1.7;padding-left:22px;margin:0 0 18px\">" +
                "<li>Open the portal and sign in with the email and temporary password above.</li>" +
                "<li>You'll be asked to <strong style=\"color:#0d3128\">set a new password</strong> — choose something only you know.</li>" +
                "<li>Pick your <strong style=\"color:#0d3128\">VIP seat</strong> — the system automatically pairs the mother and father seats.</li>" +
                "<li>Optionally, book the <strong style=\"color:#0d3128\">Guest ticket</strong> (one QR admits three) for extended family.</li>" +
              "</ol>" +

              "<div style=\"text-align:center;margin:28px 0 14px\">" +
                $"<a href=\"{E(portalUrl)}\" style=\"display:inline-block;background:#0d3128;color:#f6f1e3;padding:13px 30px;border-radius:6px;text-decoration:none;font-weight:600;font-size:14px;letter-spacing:0.08em\">Open the booking portal</a>" +
              "</div>" +
              $"<p style=\"text-align:center;font-size:12px;color:#94a3b8;margin:0 0 6px\">or paste this link: {E(portalUrl)}</p>" +

              "<p style=\"font-size:12px;color:#94a3b8;line-height:1.5;margin-top:22px\">If you didn't expect this email, please contact the school office.</p>" +
              logoBlock +
            "</div></body></html>";
    }

    /// <summary>
    /// Initial / "reset" password. Prefers the school StudentNumber for new rosters (e.g. "Ahm437079");
    /// falls back to the old date-of-birth recipe for legacy data. Always deterministic.
    /// </summary>
    public static string ComputeInitialPassword(string firstName, string? studentNumber, DateTime? dob)
    {
        var trimmed = (firstName ?? string.Empty).Trim();
        var prefix = trimmed.Length >= 3 ? trimmed[..3] : trimmed.PadRight(3, 'X');
        prefix = char.ToUpperInvariant(prefix[0]) + prefix[1..].ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(studentNumber))
            return $"{prefix}{studentNumber.Trim()}";
        if (dob.HasValue)
            return $"{prefix}{dob.Value:ddMMyyyy}";
        // No StudentNumber and no DOB — admin would have to set a password manually.
        return $"{prefix}000000";
    }
}
