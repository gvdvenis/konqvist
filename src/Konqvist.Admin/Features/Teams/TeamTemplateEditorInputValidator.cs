using FluentValidation;

namespace Konqvist.Admin.Features.Teams;

public sealed class TeamTemplateEditorInputValidator : AbstractValidator<TeamTemplateEditorInput>
{
    public TeamTemplateEditorInputValidator()
    {
        RuleFor(model => model.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(50);

        RuleFor(model => model.Color)
            .NotEmpty()
            .WithMessage("Color is required.")
            .Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage("Color must be a valid hex color code in #RRGGBB format.");
    }
}
