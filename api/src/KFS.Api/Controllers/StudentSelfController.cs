using KFS.Application.DTOs.Bookings;
using KFS.Application.DTOs.Events;
using KFS.Application.DTOs.SeatMap;
using KFS.Application.Interfaces;
using KFS.Application.Services;
using KFS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Student")]
[Route("api/v1")]
public class StudentSelfController : ControllerBase
{
    private readonly IBookingService _bookings;
    private readonly ISeatMapService _seatMap;
    private readonly IEventService _events;
    private readonly IGuestPassService _guest;
    private readonly IStudentTicketBundleService _bundle;
    private readonly ICurrentUser _currentUser;

    public StudentSelfController(IBookingService bookings, ISeatMapService seatMap,
        IEventService events, IGuestPassService guest, IStudentTicketBundleService bundle, ICurrentUser currentUser)
    {
        _bookings = bookings; _seatMap = seatMap; _events = events; _guest = guest; _bundle = bundle; _currentUser = currentUser;
    }

    private Guid StudentId => _currentUser.UserId ?? throw new KFS.Application.Common.Exceptions.AppException("unauthorized", "Not signed in.", 401);

    // The child's own Guest ticket (1 QR, admits 3) — null if not booked yet.
    [HttpGet("guest")]
    public Task<KFS.Application.DTOs.Passes.GuestPassDto?> MyGuestPass(CancellationToken ct)
        => _guest.GetForStudentAsync(StudentId, ct);

    // Book the one Guest ticket for the signed-in child.
    [HttpPost("guest")]
    public Task<KFS.Application.DTOs.Passes.GuestPassDto> BookGuestPass(CancellationToken ct)
        => _guest.BookForStudentAsync(StudentId, issuedByAdminId: null, issuedToName: null, ct);

    // Student-initiated cancel of their own guest ticket. Blocked if already scanned.
    [HttpDelete("guest")]
    public async Task<IActionResult> CancelGuestPass(CancellationToken ct)
    {
        await _guest.CancelForStudentAsync(StudentId, ct);
        return NoContent();
    }

    // Combined PDF: every parent pass + the guest ticket (if any) for the signed-in child.
    [HttpGet("me/tickets.pdf")]
    public async Task<IActionResult> TicketsBundle(CancellationToken ct)
    {
        var (bytes, fileName) = await _bundle.BuildAsync(StudentId, ct);
        return File(bytes, "application/pdf", fileName);
    }

    // Email the same combined PDF (parent + guest) to the student. Dashboard "Email all my tickets" button.
    [HttpPost("me/tickets/send-emails")]
    public async Task<IActionResult> EmailTicketsBundle(CancellationToken ct)
    {
        await _bundle.SendBundleEmailAsync(StudentId, ct);
        return Ok(new { sent = true });
    }

    [HttpGet("me")]
    public async Task<object> Me(CancellationToken ct)
    {
        var bookings = await _bookings.GetMyBookingsAsync(ct);
        return new { userId = _currentUser.UserId, email = _currentUser.Email, bookings };
    }

    [HttpGet("events/active")]
    public Task<EventDto> ActiveEvent(CancellationToken ct) => _events.GetForCurrentStudentAsync(ct);

    [HttpGet("events/{eventId:guid}/seatmap")]
    public Task<SeatMapDto> GetSeatMap(Guid eventId, [FromQuery] ZoneGroup group, CancellationToken ct)
        => _seatMap.GetAsync(eventId, group, includeOccupant: false, ct);

    [HttpGet("cart")]
    public Task<BookingDto?> GetCart(CancellationToken ct) => _bookings.GetCurrentCartAsync(ct);

    [HttpPost("cart/select")]
    public Task<BookingDto> SelectCart([FromBody] CartSelectRequest request, CancellationToken ct)
        => _bookings.SelectCartAsync(request, ct);

    [HttpDelete("cart")]
    public async Task<IActionResult> ReleaseCart(CancellationToken ct)
    {
        await _bookings.ReleaseCartAsync(ct);
        return NoContent();
    }

    [HttpPost("cart/checkout")]
    public Task<BookingDto> Checkout(CancellationToken ct) => _bookings.CheckoutAsync(ct);

    [HttpGet("bookings")]
    public Task<IReadOnlyList<BookingDto>> MyBookings(CancellationToken ct)
        => _bookings.GetMyBookingsAsync(ct);

    [HttpPost("bookings/{id:guid}/cancel")]
    public Task<BookingDto> Cancel(Guid id, CancellationToken ct) => _bookings.CancelBookingAsync(id, ct);

    [HttpPost("bookings/{id:guid}/resend-emails")]
    public async Task<IActionResult> ResendEmails(Guid id, CancellationToken ct)
    {
        await _bookings.ResendEmailsAsync(id, ct);
        return NoContent();
    }
}
