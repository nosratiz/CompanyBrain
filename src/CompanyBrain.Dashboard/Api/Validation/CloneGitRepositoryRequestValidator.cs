using CompanyBrain.Dashboard.Api.Contracts;
using FluentValidation;

namespace CompanyBrain.Dashboard.Api.Validation;

internal sealed class CloneGitRepositoryRequestValidator : AbstractValidator<CloneGitRepositoryRequest>
{
    private static readonly string[] AllowedSchemes = ["http", "https", "git", "ssh"];

    public CloneGitRepositoryRequestValidator()
    {
        RuleFor(request => request.RepositoryUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeAValidGitUrl)
            .WithMessage("Must be a valid git repository URL (http, https, git, or ssh).");

        RuleFor(request => request.TemplateName)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(@"^[a-zA-Z0-9_\-\.]+$")
            .WithMessage("Template name can only contain letters, numbers, underscores, hyphens, and dots.");

        RuleFor(request => request.Branch)
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9_\-\.\/]+$")
            .WithMessage("Branch name contains invalid characters.")
            .When(request => !string.IsNullOrWhiteSpace(request.Branch));
    }

    private static bool BeAValidGitUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Support SSH-style URLs like git@github.com:user/repo.git
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            return url.Contains(':') && url.Contains('/');
        }

        // Support standard URLs
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return AllowedSchemes.Contains(uri.Scheme.ToLowerInvariant());
        }

        return false;
    }
}
