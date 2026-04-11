using CompanyBrain.Dashboard.Scripting;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompanyBrain.Tests.Scripting;

public sealed class ScriptRunnerServiceTests
{
    private readonly ScriptRunnerService _sut;
    private readonly ILogger<ScriptRunnerService> _logger;

    public ScriptRunnerServiceTests()
    {
        _logger = Substitute.For<ILogger<ScriptRunnerService>>();
        _sut = new ScriptRunnerService(_logger);
    }

    private static ScriptExecutionContext CreateContext(
        Dictionary<string, object?>? args = null,
        bool writeEnabled = false,
        string toolName = "test-tool")
    {
        return new ScriptExecutionContext
        {
            Args = args ?? new Dictionary<string, object?>(),
            RootPath = "/tmp/test",
            IsWriteEnabled = writeEnabled,
            TenantId = Guid.NewGuid(),
            ToolName = toolName,
            CancellationToken = CancellationToken.None
        };
    }

    #region Successful Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithSimpleExpression_ShouldReturnResult()
    {
        // Arrange
        var code = "return 1 + 1;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithStringConcatenation_ShouldReturnCombined()
    {
        // Arrange
        var code = "return \"Hello\" + \" \" + \"World\";";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_WithLinq_ShouldWork()
    {
        // Arrange
        var code = "return new[] { 1, 2, 3, 4, 5 }.Where(x => x > 2).Sum();";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(12);
    }

    [Fact]
    public async Task ExecuteAsync_WithContextArgs_ShouldAccessArguments()
    {
        // Arrange
        var args = new Dictionary<string, object?> { ["name"] = "Test" };
        var code = "return $\"Hello, {Args[\"name\"]}!\";";
        var context = CreateContext(args);

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Hello, Test!");
    }

    [Fact]
    public async Task ExecuteAsync_WithGetArg_ShouldReturnTypedValue()
    {
        // Arrange
        var args = new Dictionary<string, object?> { ["count"] = 42 };
        var code = "return GetArg<int>(\"count\") * 2;";
        var context = CreateContext(args);

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(84);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullReturn_ShouldSucceed()
    {
        // Arrange
        var code = "return null;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().BeNull();
    }

    #endregion

    #region Security Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithProcessStart_ShouldReturnSecurityViolation()
    {
        // Arrange
        var code = "Process.Start(\"notepad\");";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
    }

    [Fact]
    public async Task ExecuteAsync_WithEnvironmentExit_ShouldReturnSecurityViolation()
    {
        // Arrange
        var code = "Environment.Exit(0);";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
    }

    [Fact]
    public async Task ExecuteAsync_WithAssemblyLoad_ShouldReturnSecurityViolation()
    {
        // Arrange
        var code = "Assembly.LoadFrom(\"/path/to/dll\");";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
    }

    [Fact]
    public async Task ExecuteAsync_WithThreadStart_ShouldFail()
    {
        // Arrange - Thread type is not available in script context
        var code = "new Thread(() => {}).Start();";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert - fails because Thread is not available (either compilation error or security violation)
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskRun_ShouldReturnSecurityViolation()
    {
        // Arrange
        var code = "Task.Run(() => { });";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
    }

    [Fact]
    public async Task ExecuteAsync_WithPreprocessorDirective_ShouldReturnSecurityViolation()
    {
        // Arrange
        var code = "#r \"SomeAssembly.dll\"\nreturn 1;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
    }

    [Fact]
    public async Task ExecuteAsync_WithActivatorCreateInstance_ShouldReturnSecurityViolation()
    {
        // Arrange
        var code = "Activator.CreateInstance(typeof(object));";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
    }

    [Fact]
    public async Task ExecuteAsync_WithGcCollect_ShouldReturnSecurityViolation()
    {
        // Arrange
        var code = "GC.Collect();";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Security violation");
    }

    #endregion

    #region Compilation Error Tests

    [Fact]
    public async Task ExecuteAsync_WithSyntaxError_ShouldReturnCompilationFailure()
    {
        // Arrange
        var code = "return 1 +;"; // Invalid syntax
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("compilation failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithUndeclaredVariable_ShouldReturnCompilationFailure()
    {
        // Arrange
        var code = "return undeclaredVariable;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("compilation failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithTypeMismatch_ShouldReturnCompilationFailure()
    {
        // Arrange
        var code = "int x = \"string\"; return x;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("compilation failed");
    }

    #endregion

    #region Runtime Error Tests

    [Fact]
    public async Task ExecuteAsync_WithDivisionByZero_ShouldReturnFailure()
    {
        // Arrange
        var code = "return 1 / 0;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullReferenceException_ShouldReturnFailure()
    {
        // Arrange
        var code = @"
            string s = null;
            return s.Length;
        ";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldReturnCancelledResult()
    {
        // Arrange
        var code = "return 1;";
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        
        var context = new ScriptExecutionContext
        {
            Args = new Dictionary<string, object?>(),
            RootPath = "/tmp/test",
            IsWriteEnabled = false,
            TenantId = Guid.NewGuid(),
            ToolName = "test-tool",
            CancellationToken = cts.Token
        };

        // Act
        var result = await _sut.ExecuteAsync(code, context, cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    #endregion

    #region Context Properties Tests

    [Fact]
    public async Task ExecuteAsync_ShouldAccessRootPath()
    {
        // Arrange
        var code = "return RootPath;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("/tmp/test");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAccessIsWriteEnabled()
    {
        // Arrange
        var code = "return IsWriteEnabled;";
        var context = CreateContext(writeEnabled: true);

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAccessToolName()
    {
        // Arrange
        var code = "return ToolName;";
        var context = CreateContext(toolName: "my-custom-tool");

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("my-custom-tool");
    }

    #endregion

    #region ExecutionTime Tests

    [Fact]
    public async Task ExecuteAsync_ShouldRecordExecutionTime()
    {
        // Arrange
        var code = "return 1;";
        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(code, context);

        // Assert
        result.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    #endregion
}
