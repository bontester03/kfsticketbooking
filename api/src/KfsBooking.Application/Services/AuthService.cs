using KfsBooking.Application.Common.Exceptions;
using KfsBooking.Application.DTOs.Auth;
using KfsBooking.Application.Interfaces;
using KfsBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KfsBooking.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;

    public AuthService(IApplicationDbContext db, IJwtTokenService jwt, IPasswordHasher hasher)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var emailNorm = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == emailNorm, ct);
        if (exists) throw new ConflictException("Email already registered.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = emailNorm,
            PasswordHash = _hasher.Hash(request.Password),
            Role = request.Role,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return BuildAuth(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var emailNorm = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailNorm, ct)
                   ?? throw new UnauthorizedException("Invalid credentials.");

        if (!user.IsActive) throw new UnauthorizedException("Account disabled.");
        if (!_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        return BuildAuth(user);
    }

    private AuthResponse BuildAuth(User user)
    {
        var (token, expires) = _jwt.Generate(user);
        return new AuthResponse(token, expires, user.Id, user.Email, user.FullName, user.Role);
    }
}
