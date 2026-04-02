using CompanyBrain.Api.Contracts;
using CompanyBrain.Api.Validation;
using FluentAssertions;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class SearchRequestValidatorTests
{
    private readonly SearchRequestValidator validator = new();

    [Fact]
    public void Validate_WhenMaxResultsWithinRange_ShouldSucceed()
    {
        var request = new SearchRequest("distributed systems", 10);

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenMaxResultsIsTooHigh_ShouldReturnValidationError()
    {
        var request = new SearchRequest("distributed systems", 99);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == "MaxResults");
    }

    [Fact]
    public void Validate_WhenQueryIsEmpty_ShouldReturnValidationError()
    {
        var request = new SearchRequest(string.Empty, 5);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == "Query");
    }
}