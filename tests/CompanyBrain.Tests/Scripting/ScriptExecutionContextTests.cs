using CompanyBrain.Dashboard.Scripting;
using FluentAssertions;

namespace CompanyBrain.Tests.Scripting;

public sealed class ScriptExecutionContextTests
{
    private static ScriptExecutionContext CreateContext(Dictionary<string, object?>? args = null)
    {
        return new ScriptExecutionContext
        {
            Args = args ?? new Dictionary<string, object?>(),
            RootPath = "/base/tenant",
            IsWriteEnabled = false,
            TenantId = Guid.Parse("12345678-1234-1234-1234-123456789012"),
            ToolName = "test-tool",
            CancellationToken = CancellationToken.None
        };
    }

    #region GetArg Tests

    [Fact]
    public void GetArg_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["name"] = "test" });

        // Act
        var result = context.GetArg<string>("name");

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public void GetArg_WithMissingKey_ShouldReturnDefault()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = context.GetArg<string>("missing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetArg_WithMissingKeyAndDefault_ShouldReturnProvidedDefault()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = context.GetArg("missing", "fallback");

        // Assert
        result.Should().Be("fallback");
    }

    [Fact]
    public void GetArg_WithNullValue_ShouldReturnDefault()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["key"] = null });

        // Act
        var result = context.GetArg("key", "default");

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void GetArg_WithConvertibleIntFromString_ShouldConvert()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["count"] = "42" });

        // Act
        var result = context.GetArg<int>("count");

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void GetArg_WithConvertibleDoubleFromInt_ShouldConvert()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["value"] = 10 });

        // Act
        var result = context.GetArg<double>("value");

        // Assert
        result.Should().Be(10.0);
    }

    [Fact]
    public void GetArg_WithUnconvertibleType_ShouldReturnDefault()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["value"] = "not-a-number" });

        // Act
        var result = context.GetArg("value", 99);

        // Assert
        result.Should().Be(99);
    }

    [Fact]
    public void GetArg_WithBoolFromString_ShouldConvert()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["flag"] = "true" });

        // Act
        var result = context.GetArg<bool>("flag");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetRequiredString Tests

    [Fact]
    public void GetRequiredString_WithExistingValue_ShouldReturn()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["name"] = "value" });

        // Act
        var result = context.GetRequiredString("name");

        // Assert
        result.Should().Be("value");
    }

    [Fact]
    public void GetRequiredString_WithMissingKey_ShouldThrow()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.GetRequiredString("missing");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [Fact]
    public void GetRequiredString_WithNullValue_ShouldThrow()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["key"] = null });

        // Act
        var act = () => context.GetRequiredString("key");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*missing*");
    }

    [Fact]
    public void GetRequiredString_WithNonStringValue_ShouldConvertToString()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object?> { ["number"] = 123 });

        // Act
        var result = context.GetRequiredString("number");

        // Assert
        result.Should().Be("123");
    }

    #endregion

    #region EnsureWriteEnabled Tests

    [Fact]
    public void EnsureWriteEnabled_WhenEnabled_ShouldNotThrow()
    {
        // Arrange
        var context = new ScriptExecutionContext
        {
            Args = new Dictionary<string, object?>(),
            RootPath = "/base",
            IsWriteEnabled = true,
            TenantId = Guid.NewGuid(),
            ToolName = "write-tool",
            CancellationToken = CancellationToken.None
        };

        // Act
        var act = () => context.EnsureWriteEnabled();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureWriteEnabled_WhenDisabled_ShouldThrowUnauthorized()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.EnsureWriteEnabled();

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*write permission*");
    }

    #endregion

    #region ResolvePath Tests

    [Fact]
    public void ResolvePath_WithSimpleRelativePath_ShouldCombine()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = context.ResolvePath("documents/file.txt");

        // Assert
        result.Should().Be(Path.Combine("/base/tenant", "documents/file.txt"));
    }

    [Fact]
    public void ResolvePath_WithDotPath_ShouldNormalize()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = context.ResolvePath("./file.txt");

        // Assert
        result.Should().EndWith("file.txt");
    }

    [Fact]
    public void ResolvePath_WithTraversalAttempt_ShouldThrow()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.ResolvePath("../../../etc/passwd");

        // Assert
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void ResolvePath_WithAbsolutePath_ShouldThrow()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var act = () => context.ResolvePath("/etc/passwd");

        // Assert
        act.Should().Throw<UnauthorizedAccessException>();
    }

    #endregion
}
