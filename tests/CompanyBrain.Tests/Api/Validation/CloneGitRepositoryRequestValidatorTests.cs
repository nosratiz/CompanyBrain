using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Api.Validation;
using FluentAssertions;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class CloneGitRepositoryRequestValidatorTests
{
    private readonly CloneGitRepositoryRequestValidator _validator = new();

    #region Valid Requests

    [Theory]
    [InlineData("https://github.com/user/repo.git", "my-template", null)]
    [InlineData("http://example.com/repo.git", "my-template", null)]
    [InlineData("git://github.com/user/repo.git", "my-template", null)]
    [InlineData("ssh://git@github.com/user/repo.git", "my-template", null)]
    [InlineData("git@github.com:user/repo.git", "my-template", null)]
    [InlineData("https://github.com/user/repo.git", "my-template", "main")]
    [InlineData("https://github.com/user/repo.git", "my_template.v1", "feature/new-branch")]
    public void Validate_WithValidRequest_ShouldSucceed(string url, string name, string? branch)
    {
        var request = new CloneGitRepositoryRequest(url, name, branch);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region RepositoryUrl Validation

    [Fact]
    public void Validate_WhenUrlIsEmpty_ShouldFail()
    {
        var request = new CloneGitRepositoryRequest("", "my-template", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RepositoryUrl");
    }

    [Fact]
    public void Validate_WhenUrlIsFtpScheme_ShouldFail()
    {
        var request = new CloneGitRepositoryRequest("ftp://example.com/repo.git", "my-template", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RepositoryUrl");
    }

    [Fact]
    public void Validate_WhenUrlExceedsMaxLength_ShouldFail()
    {
        var longUrl = "https://github.com/" + new string('a', 2048);
        var request = new CloneGitRepositoryRequest(longUrl, "my-template", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RepositoryUrl");
    }

    [Fact]
    public void Validate_WhenSshUrlMissingColon_ShouldFail()
    {
        var request = new CloneGitRepositoryRequest("git@github.com/user/repo.git", "my-template", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenSshUrlMissingSlash_ShouldFail()
    {
        var request = new CloneGitRepositoryRequest("git@github.com:userrepo.git", "my-template", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region TemplateName Validation

    [Fact]
    public void Validate_WhenTemplateNameIsEmpty_ShouldFail()
    {
        var request = new CloneGitRepositoryRequest("https://github.com/user/repo.git", "", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TemplateName");
    }

    [Fact]
    public void Validate_WhenTemplateNameHasInvalidCharacters_ShouldFail()
    {
        var request = new CloneGitRepositoryRequest("https://github.com/user/repo.git", "my template!", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TemplateName");
    }

    [Fact]
    public void Validate_WhenTemplateNameExceedsMaxLength_ShouldFail()
    {
        var longName = new string('a', 201);
        var request = new CloneGitRepositoryRequest("https://github.com/user/repo.git", longName, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TemplateName");
    }

    #endregion

    #region Branch Validation

    [Fact]
    public void Validate_WhenBranchHasInvalidCharacters_ShouldFail()
    {
        var request = new CloneGitRepositoryRequest("https://github.com/user/repo.git", "my-template", "branch with spaces");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Branch");
    }

    [Fact]
    public void Validate_WhenBranchExceedsMaxLength_ShouldFail()
    {
        var longBranch = new string('a', 101);
        var request = new CloneGitRepositoryRequest("https://github.com/user/repo.git", "my-template", longBranch);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Branch");
    }

    [Fact]
    public void Validate_WhenBranchIsWhitespace_ShouldSkipValidation()
    {
        // Whitespace branch is treated as "not provided" → validation skipped
        var request = new CloneGitRepositoryRequest("https://github.com/user/repo.git", "my-template", "   ");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
