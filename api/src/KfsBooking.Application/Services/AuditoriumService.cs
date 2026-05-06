using KfsBooking.Application.Common.Exceptions;
using KfsBooking.Application.DTOs.Auditoriums;
using KfsBooking.Application.Interfaces;
using KfsBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KfsBooking.Application.Services;

public class AuditoriumService : IAuditoriumService
{
    private readonly IApplicationDbContext _db;

    public AuditoriumService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<AuditoriumDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Auditoriums
            .OrderBy(a => a.Name)
            .Select(a => new AuditoriumDto(a.Id, a.Name, a.Location, a.Capacity, a.Description, a.IsActive))
            .ToListAsync(ct);
    }

    public async Task<AuditoriumDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var a = await _db.Auditoriums.FindAsync(new object?[] { id }, ct)
                ?? throw new NotFoundException(nameof(Auditorium), id);
        return new AuditoriumDto(a.Id, a.Name, a.Location, a.Capacity, a.Description, a.IsActive);
    }

    public async Task<AuditoriumDto> CreateAsync(CreateAuditoriumRequest request, CancellationToken ct = default)
    {
        var entity = new Auditorium
        {
            Name = request.Name.Trim(),
            Location = request.Location.Trim(),
            Capacity = request.Capacity,
            Description = request.Description?.Trim()
        };
        _db.Auditoriums.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new AuditoriumDto(entity.Id, entity.Name, entity.Location, entity.Capacity, entity.Description, entity.IsActive);
    }

    public async Task<AuditoriumDto> UpdateAsync(Guid id, UpdateAuditoriumRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Auditoriums.FindAsync(new object?[] { id }, ct)
                     ?? throw new NotFoundException(nameof(Auditorium), id);

        entity.Name = request.Name.Trim();
        entity.Location = request.Location.Trim();
        entity.Capacity = request.Capacity;
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new AuditoriumDto(entity.Id, entity.Name, entity.Location, entity.Capacity, entity.Description, entity.IsActive);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Auditoriums.FindAsync(new object?[] { id }, ct)
                     ?? throw new NotFoundException(nameof(Auditorium), id);
        _db.Auditoriums.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
