using CompanyBrain.Application.Results;
using CompanyBrain.Dashboard.Api.ResultMapping;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CompanyBrain.Tests.Api;

public sealed class FluentResultHttpMapperTests
{
    #region Error Status Code Mapping Tests

    [Fact]
    public void ToProblemResult_WhenValidationAppError_ShouldReturn400()
    {
        var result = Result.Fail(new ValidationAppError("Invalid input"));

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(400);
    }

    [Fact]
    public void ToProblemResult_WhenNotFoundAppError_ShouldReturn404()
    {
        var result = Result.Fail(new NotFoundAppError("Resource not found"));

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(404);
    }

    [Fact]
    public void ToProblemResult_WhenUpstreamAppError_ShouldReturn502()
    {
        var result = Result.Fail(new UpstreamAppError("Upstream failed"));

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(502);
    }

    [Fact]
    public void ToProblemResult_WhenGenericFluentError_ShouldReturn400()
    {
        var result = Result.Fail("Something went wrong");

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(400);
    }

    #endregion

    #region Title Mapping Tests

    [Fact]
    public void ToProblemResult_WhenNotFound_ShouldHaveNotFoundTitle()
    {
        var result = Result.Fail(new NotFoundAppError("not found"));

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.ProblemDetails.Title.Should().Be("Resource not found");
    }

    [Fact]
    public void ToProblemResult_WhenUpstream_ShouldHaveUpstreamTitle()
    {
        var result = Result.Fail(new UpstreamAppError("bad gateway"));

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.ProblemDetails.Title.Should().Be("Upstream request failed");
    }

    [Fact]
    public void ToProblemResult_WhenValidation_ShouldHaveRequestFailedTitle()
    {
        var result = Result.Fail(new ValidationAppError("invalid"));

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.ProblemDetails.Title.Should().Be("Request failed");
    }

    #endregion

    #region Detail Message Tests

    [Fact]
    public void ToProblemResult_ShouldIncludeErrorMessagesInDetail()
    {
        var result = Result.Fail(new ValidationAppError("Field X is required"));

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.ProblemDetails.Detail.Should().Contain("Field X is required");
    }

    [Fact]
    public void ToProblemResult_WithMultipleErrors_ShouldCombineMessages()
    {
        var result = Result.Fail("Error 1").WithError("Error 2");

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.ProblemDetails.Detail.Should().Contain("Error 1");
        problem.ProblemDetails.Detail.Should().Contain("Error 2");
    }

    [Fact]
    public void ToProblemResult_ShouldDeduplicateIdenticalMessages()
    {
        var result = Result.Fail("Duplicate error").WithError("Duplicate error");

        var httpResult = result.ToProblemResult();

        var problem = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        var occurrences = problem.ProblemDetails.Detail!
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Count(l => l == "Duplicate error");
        occurrences.Should().Be(1);
    }

    #endregion
}
