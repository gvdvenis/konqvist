using FluentValidation;

namespace Konqvist.Admin.Features.Rounds;

public sealed class RoundTemplateEditorInputValidator : AbstractValidator<RoundTemplateEditorInput>
{
    public RoundTemplateEditorInputValidator()
    {
        RuleFor(model => model.Stake)
            .NotEmpty()
            .WithMessage("Stake is required.")
            .MaximumLength(500)
            .WithMessage("Stake must be 500 characters or fewer.");
    }
}
