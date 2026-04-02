using CompanyBrain.Api.Contracts;
using CompanyBrain.Api.Validation;
using FluentAssertions;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class IngestWikiRequestValidatorTests
{
    private readonly IngestWikiRequestValidator validator = new();

    [Fact]
    public void Validate_WhenRequestIsValid_ShouldSucceed()
    {
        var request = new IngestWikiRequest("https://example.com/wiki/page", "engineering-wiki");

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenUrlIsNotHttpOrHttps_ShouldReturnValidationError()
    {
        var request = new IngestWikiRequest("ftp://example.com/wiki/page", "engineering-wiki");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == "Url");
    }

    [Fact]
    public void Validate_WhenNameIsMissing_ShouldReturnValidationError()
    {
        var request = new IngestWikiRequest("https://example.com/wiki/page", string.Empty);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == "Name");
    }
}