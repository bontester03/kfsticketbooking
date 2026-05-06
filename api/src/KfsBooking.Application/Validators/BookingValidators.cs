using FluentValidation;
using KfsBooking.Application.DTOs.Auditoriums;
using KfsBooking.Application.DTOs.Bookings;

namespace KfsBooking.Application.Validators;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.AuditoriumId).NotEmpty();
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(500);
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty()
            .GreaterThan(x => x.StartTime).WithMessage("EndTime must be after StartTime.");
    }
}

public class CreateAuditoriumRequestValidator : AbstractValidator<CreateAuditoriumRequest>
{
    public CreateAuditoriumRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(5000);
    }
}

public class UpdateAuditoriumRequestValidator : AbstractValidator<UpdateAuditoriumRequest>
{
    public UpdateAuditoriumRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(5000);
    }
}
