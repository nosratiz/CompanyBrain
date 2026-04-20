using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Api.Validation;
using FluentAssertions;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class IngestPathRequestValidatorTests
{
    private readonly IngestPathRequestValidator _validator = new();

    #region Valid Requests

    [Theory]
    [InlineData("/path/to/docs", null)]
    [InlineData("/path/to/docs", "MyDocs")]
    [InlineData("C:\\Users\\docs", null)]
    public void Validate_WithValidRequest_ShouldSucceed(string localPath, string? name)
    {
        var request = new IngestPathRequest(localPath, name);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region LocalPath Validation

    [Fact]
    public void Validate_WhenLocalPathIsEmpty_ShouldFail()
    {
        var request = new IngestPathRequest("", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LocalPath");
    }

    [Fact]
    public void Validate_WhenLocalPathExceedsMaxLength_ShouldFail()
    {
        var longPath = "/" + new string('a', 1024);
        var request = new IngestPathRequest(longPath, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LocalPath");
    }

    #endregion

    #region Name Validation

    [Fact]
    public void Validate_WhenNameExceedsMaxLength_ShouldFail()
    {
        var longName = new string('a', 201);
        var request = new IngestPathRequest("/valid/path", longName);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhenNameIsWhitespace_ShouldSkipNameValidation()
    {
        // Whitespace name is treated as null → skip validation
        var request = new IngestPathRequest("/valid/path", "   ");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenNameIsNull_ShouldSkipNameValidation()
    {
        var request = new IngestPathRequest("/valid/path", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
