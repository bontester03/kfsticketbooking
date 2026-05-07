using KFS.Application.DTOs.Bookings;
using KFS.Application.DTOs.SeatMap;
using KFS.Application.Interfaces;
using KFS.Application.Services;
using KFS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public class AdminBookingsController : ControllerBase
{
    private readonly IBookingService _bookings;
    private readonly ISeatMapService _seatMap;
    private readonly IApplicationDbContext _db;
    private readonly IBlobStorage _blobs;

    public AdminBookingsController(IBookingService bookings, ISeatMapService seatMap,
        IApplicationDbContext db, IBlobStorage blobs)
    {
        _bookings = bookings; _seatMap = seatMap; _db = db; _blobs = blobs;
    }

    [HttpGet("bookings")]
    public async Task<IReadOnlyList<BookingDto>> List([FromQuery] ZoneGroup? group, [FromQuery] BookingStatus? status, CancellationToken ct)
    {
        var q = _db.Bookings
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .AsQueryable();
        if (group.HasValue) q = q.Where(b => b.GroupChosen == group);
        if (status.HasValue) q = q.Where(b => b.Status == status);
        var bookings = await q.OrderByDescending(b => b.CreatedAt).Take(500).ToListAsync(ct);
        return bookings.Select(b => new BookingDto(b.Id, b.StudentId, b.Status, b.GroupChosen,
            b.CreatedAt, b.ConfirmedAt, b.CancelledAt, b.RebookWindowExpiresAt,
            b.Items.Select(i => new BookingItemDto(i.Id, i.SeatId,
                Application.Services.BookingService.BlockLabel(i.Zone?.Code ?? ZoneCode.VIPAF),
                i.Seat?.RowLabel ?? "", i.Seat?.SeatNumber ?? 0, i.Seat?.FullLabel ?? "",
                i.ParentRole, i.TicketNumber,
                i.QrCodeImageUrl is null ? null : _blobs.RefreshReadUrl(i.QrCodeImageUrl),
                i.EmailSent, i.HoldExpiresAt)).ToList())).ToList();
    }

    [HttpGet("seatmap")]
    public async Task<SeatMapDto> SeatMap([FromQuery] ZoneGroup group, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct)
            ?? throw new KFS.Application.Common.Exceptions.NotFoundException("Event", "active");
        return await _seatMap.GetAsync(ev.Id, group, includeOccupant: true, ct);
    }

    [HttpPost("bookings/{id:guid}/force-cancel")]
    public async Task<IActionResult> ForceCancel(Guid id, CancellationToken ct)
    {
        await _bookings.ForceCancelAsync(id, ct);
        return NoContent();
    }
}
