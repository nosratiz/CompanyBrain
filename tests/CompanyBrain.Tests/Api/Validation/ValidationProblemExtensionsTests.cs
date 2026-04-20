using CompanyBrain.Dashboard.Api.Validation;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class ValidationProblemExtensionsTests
{
    [Fact]
    public void ToValidationProblem_WithSingleError_ShouldReturnValidationProblem()
    {
        var validationResult = new ValidationResult(
        [
            new ValidationFailure("FieldName", "Field is required")
        ]);

        var result = validationResult.ToValidationProblem();

        result.Should().NotBeNull();
        result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public void ToValidationProblem_ShouldGroupErrorsByPropertyName()
    {
        var validationResult = new ValidationResult(
        [
            new ValidationFailure("Email", "Email is required"),
            new ValidationFailure("Email", "Email is invalid"),
            new ValidationFailure("Name", "Name is required")
        ]);

        var result = validationResult.ToValidationProblem().Should().BeOfType<ValidationProblem>().Subject;

        result.ProblemDetails.Errors.Should().ContainKey("Email");
        result.ProblemDetails.Errors["Email"].Should().HaveCount(2);
        result.ProblemDetails.Errors.Should().ContainKey("Name");
        result.ProblemDetails.Errors["Name"].Should().HaveCount(1);
    }

    [Fact]
    public void ToValidationProblem_ShouldDeduplicateIdenticalMessages()
    {
        var validationResult = new ValidationResult(
        [
            new ValidationFailure("Field", "Error message"),
            new ValidationFailure("Field", "Error message")
        ]);

        var result = validationResult.ToValidationProblem().Should().BeOfType<ValidationProblem>().Subject;

        result.ProblemDetails.Errors["Field"].Should().HaveCount(1);
        result.ProblemDetails.Errors["Field"].Should().Contain("Error message");
    }

    [Fact]
    public void ToValidationProblem_WhenPropertyNameIsEmpty_ShouldUseRequestKey()
    {
        var failure = new ValidationFailure("", "Some global error");
        var validationResult = new ValidationResult([failure]);

        var result = validationResult.ToValidationProblem().Should().BeOfType<ValidationProblem>().Subject;

        result.ProblemDetails.Errors.Should().ContainKey("request");
        result.ProblemDetails.Errors["request"].Should().Contain("Some global error");
    }

    [Fact]
    public void ToValidationProblem_WhenPropertyNameIsWhitespace_ShouldUseRequestKey()
    {
        var failure = new ValidationFailure("   ", "Some global error");
        var validationResult = new ValidationResult([failure]);

        var result = validationResult.ToValidationProblem().Should().BeOfType<ValidationProblem>().Subject;

        result.ProblemDetails.Errors.Should().ContainKey("request");
    }

    [Fact]
    public void ToValidationProblem_WithMultipleFields_ShouldGroupCorrectly()
    {
        var failures = new List<ValidationFailure>
        {
            new("FirstName", "Required"),
            new("LastName", "Required"),
            new("Email", "Invalid format"),
            new("Email", "Too long")
        };
        var validationResult = new ValidationResult(failures);

        var result = validationResult.ToValidationProblem().Should().BeOfType<ValidationProblem>().Subject;

        result.ProblemDetails.Errors.Should().HaveCount(3);
        result.ProblemDetails.Errors["Email"].Should().HaveCount(2);
    }
}
