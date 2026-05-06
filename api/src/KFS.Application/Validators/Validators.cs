using FluentValidation;
using KFS.Application.DTOs.Auth;
using KFS.Application.DTOs.Bookings;
using KFS.Application.DTOs.Events;
using KFS.Application.DTOs.Passes;
using KFS.Application.DTOs.Scan;
using KFS.Application.DTOs.Students;
using KFS.Application.DTOs.Reminders;

namespace KFS.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public class CartSelectRequestValidator : AbstractValidator<CartSelectRequest>
{
    public CartSelectRequestValidator()
    {
        RuleFor(x => x.RowLabel).NotEmpty().MaximumLength(4);
        RuleFor(x => x.SeatNumber).GreaterThan(0).LessThanOrEqualTo(19);
    }
}

public class GeneratePassesRequestValidator : AbstractValidator<GeneratePassesRequest>
{
    public GeneratePassesRequestValidator()
    {
        RuleFor(x => x.Count).GreaterThan(0).LessThanOrEqualTo(1000);
    }
}

public class ScanRequestValidator : AbstractValidator<ScanRequest>
{
    public ScanRequestValidator()
    {
        RuleFor(x => x.QrPayload).NotEmpty();
        RuleFor(x => x.EventToken).NotEmpty();
    }
}

public class UpdateEventRequestValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Venue).NotEmpty().MaximumLength(160);
        RuleFor(x => x.VenueAddress).NotEmpty().MaximumLength(280);
        RuleFor(x => x.CartHoldMinutes).GreaterThan(0).LessThanOrEqualTo(120);
        RuleFor(x => x.CancellationWindowMinutes).GreaterThan(0).LessThanOrEqualTo(120);
        RuleFor(x => x.BookingClosesAt).GreaterThan(x => x.BookingOpensAt);
    }
}

public class UpdatePassRequestValidator : AbstractValidator<UpdatePassRequest>
{
    public UpdatePassRequestValidator() => RuleFor(x => x.IssuedToName).MaximumLength(180);
}

public class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequest> { }

public class SendUnbookedReminderRequestValidator : AbstractValidator<SendUnbookedReminderRequest>
{
    public SendUnbookedReminderRequestValidator()
    {
        RuleFor(x => x.CustomSubject).MaximumLength(160);
    }
}
