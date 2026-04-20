using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Api.Validation;
using FluentAssertions;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class IngestWikiBatchRequestValidatorTests
{
    private readonly IngestWikiBatchRequestValidator _validator = new();

    #region Valid Requests

    [Theory]
    [InlineData("https://example.com/wiki", null)]
    [InlineData("http://example.com/wiki", null)]
    [InlineData("https://example.com/wiki", "a.wiki-link")]
    public void Validate_WithValidRequest_ShouldSucceed(string url, string? linkSelector)
    {
        var request = new IngestWikiBatchRequest(url, linkSelector);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Url Validation

    [Fact]
    public void Validate_WhenUrlIsEmpty_ShouldFail()
    {
        var request = new IngestWikiBatchRequest("", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Url");
    }

    [Fact]
    public void Validate_WhenUrlIsFtpScheme_ShouldFail()
    {
        var request = new IngestWikiBatchRequest("ftp://example.com/wiki", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Url" &&
            e.ErrorMessage.Contains("http or https"));
    }

    [Fact]
    public void Validate_WhenUrlIsRelative_ShouldFail()
    {
        var request = new IngestWikiBatchRequest("/relative/path", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Url");
    }

    [Fact]
    public void Validate_WhenUrlIsNotUri_ShouldFail()
    {
        var request = new IngestWikiBatchRequest("not a url", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Url");
    }

    #endregion

    #region LinkSelector Validation

    [Fact]
    public void Validate_WhenLinkSelectorExceedsMaxLength_ShouldFail()
    {
        var longSelector = new string('a', 501);
        var request = new IngestWikiBatchRequest("https://example.com/wiki", longSelector);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LinkSelector");
    }

    [Fact]
    public void Validate_WhenLinkSelectorIsNull_ShouldSkipValidation()
    {
        var request = new IngestWikiBatchRequest("https://example.com/wiki", null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenLinkSelectorIsWithinMaxLength_ShouldSucceed()
    {
        var selector = new string('a', 500);
        var request = new IngestWikiBatchRequest("https://example.com/wiki", selector);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
