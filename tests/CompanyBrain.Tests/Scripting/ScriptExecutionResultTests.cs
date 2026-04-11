using CompanyBrain.Dashboard.Scripting;
using FluentAssertions;

namespace CompanyBrain.Tests.Scripting;

public sealed class ScriptExecutionResultTests
{
    #region Factory Method Tests

    [Fact]
    public void Ok_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = ScriptExecutionResult.Ok("output", TimeSpan.FromMilliseconds(100));

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.Error.Should().BeNull();
        result.ExecutionTime.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Ok_WithNullOutput_ShouldSucceed()
    {
        // Act
        var result = ScriptExecutionResult.Ok(null, TimeSpan.FromMilliseconds(50));

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().BeNull();
    }

    [Fact]
    public void Ok_WithComplexObject_ShouldStoreObject()
    {
        // Arrange
        var data = new { Name = "Test", Value = 42 };

        // Act
        var result = ScriptExecutionResult.Ok(data, TimeSpan.FromMilliseconds(50));

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void Fail_ShouldCreateFailureResult()
    {
        // Act
        var result = ScriptExecutionResult.Fail("Something went wrong", TimeSpan.FromMilliseconds(200));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Something went wrong");
        result.Output.Should().BeNull();
        result.ExecutionTime.Should().Be(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void Fail_WithException_ShouldIncludeDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("Inner error");

        // Act
        var result = ScriptExecutionResult.Fail("Error occurred", TimeSpan.FromMilliseconds(100), exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Error occurred");
        result.ExceptionDetails.Should().Contain("InvalidOperationException");
        result.ExceptionDetails.Should().Contain("Inner error");
    }

    [Fact]
    public void Timeout_ShouldCreateTimeoutResult()
    {
        // Act
        var result = ScriptExecutionResult.Timeout(TimeSpan.FromSeconds(5));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timed out");
        result.Error.Should().Contain("5 second");
        result.ExecutionTime.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SecurityViolation_ShouldCreateSecurityResult()
    {
        // Act
        var result = ScriptExecutionResult.SecurityViolation("Blocked dangerous code", TimeSpan.FromMilliseconds(10));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
        result.Error.Should().Contain("Blocked dangerous code");
    }

    #endregion

    #region GetOutputString Tests

    [Fact]
    public void GetOutputString_WhenFailed_ShouldReturnError()
    {
        // Arrange
        var result = ScriptExecutionResult.Fail("Error message", TimeSpan.Zero);

        // Act
        var output = result.GetOutputString();

        // Assert
        output.Should().Be("Error message");
    }

    [Fact]
    public void GetOutputString_WhenFailedWithNullError_ShouldReturnUnknown()
    {
        // Arrange
        var result = new ScriptExecutionResult
        {
            Success = false,
            Error = null,
            ExecutionTime = TimeSpan.Zero
        };

        // Act
        var output = result.GetOutputString();

        // Assert
        output.Should().Be("Unknown error occurred.");
    }

    [Fact]
    public void GetOutputString_WithNullOutput_ShouldReturnCompletedMessage()
    {
        // Arrange
        var result = ScriptExecutionResult.Ok(null, TimeSpan.Zero);

        // Act
        var output = result.GetOutputString();

        // Assert
        output.Should().Be("Script completed with no output.");
    }

    [Fact]
    public void GetOutputString_WithStringOutput_ShouldReturnDirectly()
    {
        // Arrange
        var result = ScriptExecutionResult.Ok("Direct string output", TimeSpan.Zero);

        // Act
        var output = result.GetOutputString();

        // Assert
        output.Should().Be("Direct string output");
    }

    [Fact]
    public void GetOutputString_WithObjectOutput_ShouldSerializeToJson()
    {
        // Arrange
        var data = new { Name = "Test", Count = 5 };
        var result = ScriptExecutionResult.Ok(data, TimeSpan.Zero);

        // Act
        var output = result.GetOutputString();

        // Assert
        output.Should().Contain("\"Name\"");
        output.Should().Contain("\"Test\"");
        output.Should().Contain("\"Count\"");
        output.Should().Contain("5");
    }

    [Fact]
    public void GetOutputString_WithIntegerOutput_ShouldSerialize()
    {
        // Arrange
        var result = ScriptExecutionResult.Ok(42, TimeSpan.Zero);

        // Act
        var output = result.GetOutputString();

        // Assert
        output.Should().Be("42");
    }

    [Fact]
    public void GetOutputString_WithListOutput_ShouldSerializeAsArray()
    {
        // Arrange
        var result = ScriptExecutionResult.Ok(new[] { 1, 2, 3 }, TimeSpan.Zero);

        // Act
        var output = result.GetOutputString();

        // Assert
        output.Should().Be("[1,2,3]");
    }

    #endregion
}
