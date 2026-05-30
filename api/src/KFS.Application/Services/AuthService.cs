using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Auth;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUser _currentUser;

    public AuthService(IApplicationDbContext db, IJwtTokenService jwt, IPasswordHasher hasher, ICurrentUser currentUser)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
        _currentUser = currentUser;
    }

    public async Task<AuthResponse> StudentLoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Email == email, ct)
            ?? throw new UnauthorizedException("Invalid credentials.");
        if (!student.IsActive) throw new UnauthorizedException("Account disabled.");
        if (!_hasher.Verify(request.Password, student.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        return await IssueAndPersistAsync(student.Id, UserType.Student, student.Email,
            $"{student.FirstName} {student.LastName}", new[] { "Student" }, student.MustChangePassword, ct,
            assignedGroup: student.AssignedGroup.HasValue ? (int?)student.AssignedGroup.Value : null,
            eventId: student.EventId);
    }

    public async Task<AuthResponse> AdminLoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Email == email, ct)
            ?? throw new UnauthorizedException("Invalid credentials.");
        if (!admin.IsActive) throw new UnauthorizedException("Account disabled.");
        if (!_hasher.Verify(request.Password, admin.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        return await IssueAndPersistAsync(admin.Id, UserType.Admin, admin.Email,
            admin.FullName, new[] { "Admin" }, admin.MustChangePassword, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (stored.RevokedAt != null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token expired.");

        // Rotate: revoke the old one, issue a new one chained via ReplacedByTokenId.
        stored.RevokedAt = DateTime.UtcNow;

        string email; string name; string[] roles; bool mustChange; int? assignedGroup = null; Guid? eventId = null;
        if (stored.UserType == UserType.Student)
        {
            var s = await _db.Students.FindAsync(new object[] { stored.UserId }, ct)
                ?? throw new UnauthorizedException();
            email = s.Email; name = $"{s.FirstName} {s.LastName}"; roles = new[] { "Student" }; mustChange = s.MustChangePassword;
            assignedGroup = s.AssignedGroup.HasValue ? (int?)s.AssignedGroup.Value : null;
            eventId = s.EventId;
        }
        else
        {
            var a = await _db.Admins.FindAsync(new object[] { stored.UserId }, ct)
                ?? throw new UnauthorizedException();
            email = a.Email; name = a.FullName; roles = new[] { "Admin" }; mustChange = a.MustChangePassword;
        }

        var resp = await IssueAndPersistAsync(stored.UserId, stored.UserType, email, name, roles, mustChange, ct, replacedById: stored.Id, assignedGroup: assignedGroup, eventId: eventId);
        await _db.SaveChangesAsync(ct);
        return resp;
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var userType = _currentUser.UserType ?? throw new UnauthorizedException();

        if (userType == UserType.Student)
        {
            var s = await _db.Students.FindAsync(new object[] { userId }, ct) ?? throw new UnauthorizedException();
            if (!_hasher.Verify(request.CurrentPassword, s.PasswordHash))
                throw new UnauthorizedException("Current password is incorrect.");
            s.PasswordHash = _hasher.Hash(request.NewPassword);
            s.MustChangePassword = false;
        }
        else
        {
            var a = await _db.Admins.FindAsync(new object[] { userId }, ct) ?? throw new UnauthorizedException();
            if (!_hasher.Verify(request.CurrentPassword, a.PasswordHash))
                throw new UnauthorizedException("Current password is incorrect.");
            a.PasswordHash = _hasher.Hash(request.NewPassword);
            a.MustChangePassword = false;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Email == email, ct);
        if (student is null) return; // silent no-op so the endpoint can't enumerate accounts

        var token = Guid.NewGuid().ToString("N");
        _db.PasswordResets.Add(new PasswordReset
        {
            StudentId = student.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        await _db.SaveChangesAsync(ct);
        // Email send wired in API layer / orchestration where IEmailService is registered.
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var reset = await _db.PasswordResets.FirstOrDefaultAsync(r => r.Token == request.Token && !r.Used, ct)
            ?? throw new AppException("invalid_token", "Reset token is invalid.", 400);

        if (reset.ExpiresAt <= DateTime.UtcNow)
            throw new AppException("expired_token", "Reset token has expired.", 400);

        var student = await _db.Students.FindAsync(new object[] { reset.StudentId }, ct)
            ?? throw new NotFoundException("Student", reset.StudentId);

        student.PasswordHash = _hasher.Hash(request.NewPassword);
        student.MustChangePassword = false;
        reset.Used = true;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueAndPersistAsync(
        Guid userId, UserType userType, string email, string displayName, IEnumerable<string> roles,
        bool mustChange, CancellationToken ct, Guid? replacedById = null, int? assignedGroup = null,
        Guid? eventId = null)
    {
        var pair = _jwt.Issue(userId, userType, email, roles, eventId);
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            UserType = userType,
            TokenHash = _jwt.HashRefreshToken(pair.RefreshToken),
            ExpiresAt = pair.RefreshExpiresAt
        });
        await _db.SaveChangesAsync(ct);
        return new AuthResponse(pair.AccessToken, pair.AccessExpiresAt, pair.RefreshToken, pair.RefreshExpiresAt,
            userId, userType, email, displayName, mustChange, assignedGroup);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
