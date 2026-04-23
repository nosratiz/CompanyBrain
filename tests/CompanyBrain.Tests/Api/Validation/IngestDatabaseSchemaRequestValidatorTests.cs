using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Api.Validation;
using FluentAssertions;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class IngestDatabaseSchemaRequestValidatorTests
{
    private readonly IngestDatabaseSchemaRequestValidator _validator = new();

    #region Valid Requests

    [Theory]
    [InlineData("Server=localhost;Database=mydb;", "MySchema", "SqlServer")]
    [InlineData("Host=localhost;Database=mydb;", "MySchema", "PostgreSql")]
    [InlineData("Server=localhost;Database=mydb;", "MySchema", "MySql")]
    public void Validate_WithValidRequest_ShouldSucceed(string connectionString, string name, string provider)
    {
        var request = new IngestDatabaseSchemaRequest(connectionString, name, provider);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region ConnectionString Validation

    [Fact]
    public void Validate_WhenConnectionStringIsEmpty_ShouldFail()
    {
        var request = new IngestDatabaseSchemaRequest("", "MySchema", "SqlServer");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ConnectionString");
    }

    #endregion

    #region Name Validation

    [Fact]
    public void Validate_WhenNameIsEmpty_ShouldFail()
    {
        var request = new IngestDatabaseSchemaRequest("Server=localhost", "", "SqlServer");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhenNameExceedsMaxLength_ShouldFail()
    {
        var longName = new string('a', 201);
        var request = new IngestDatabaseSchemaRequest("Server=localhost", longName, "SqlServer");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    #endregion

    #region Provider Validation

    [Fact]
    public void Validate_WhenProviderIsEmpty_ShouldFail()
    {
        var request = new IngestDatabaseSchemaRequest("Server=localhost", "MySchema", "");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Provider");
    }

    [Fact]
    public void Validate_WhenProviderIsInvalid_ShouldFail()
    {
        var request = new IngestDatabaseSchemaRequest("Server=localhost", "MySchema", "Oracle");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Provider" &&
            e.ErrorMessage.Contains("SqlServer"));
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSql")]
    [InlineData("MySql")]
    public void Validate_WithAllowedProviders_ShouldSucceed(string provider)
    {
        var request = new IngestDatabaseSchemaRequest("Server=localhost", "MySchema", provider);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
