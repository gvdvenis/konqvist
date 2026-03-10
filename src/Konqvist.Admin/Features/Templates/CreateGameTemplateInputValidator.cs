using FluentValidation;

namespace Konqvist.Admin.Features.Templates;

public sealed class CreateGameTemplateInputValidator : AbstractValidator<CreateGameTemplateInput>
{
    public CreateGameTemplateInputValidator()
    {
        RuleFor(model => model.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(model => model.LocationUpdateIntervalSeconds)
            .GreaterThan(0)
            .WithMessage("Location update interval must be greater than 0.");

        RuleFor(model => model.MinLocationUpdateIntervalSeconds)
            .GreaterThan(0)
            .WithMessage("Minimum location update interval must be greater than 0.");

        RuleFor(model => model.VotingDurationSeconds)
            .GreaterThan(0)
            .WithMessage("Voting duration must be greater than 0.");

        RuleFor(model => model.PredictionBonusPoints)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Prediction bonus points cannot be negative.");

        RuleFor(model => model.VoteTimeoutPenalty)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Vote timeout penalty cannot be negative.");

        RuleFor(model => model.DistrictCaptureRadiusMeters)
            .GreaterThan(0)
            .WithMessage("District capture radius must be greater than 0.");

        RuleFor(model => model.LocationUpdateIntervalSeconds)
            .GreaterThanOrEqualTo(model => model.MinLocationUpdateIntervalSeconds)
            .WithMessage("Location update interval must be greater than or equal to minimum location update interval.");
    }
}
