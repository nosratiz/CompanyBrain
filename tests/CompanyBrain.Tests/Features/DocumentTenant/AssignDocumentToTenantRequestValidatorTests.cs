using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using CompanyBrain.Dashboard.Features.DocumentTenant.Validators;
using FluentAssertions;

namespace CompanyBrain.Tests.Features.DocumentTenant;

public sealed class AssignDocumentToTenantRequestValidatorTests
{
    private readonly AssignDocumentToTenantRequestValidator _validator = new();

    #region Valid Requests

    [Fact]
    public void Validate_WithValidRequest_ShouldSucceed()
    {
        var request = new AssignDocumentToTenantRequest("document.pdf", Guid.NewGuid(), "Acme Corp");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region FileName Validation

    [Fact]
    public void Validate_WhenFileNameIsEmpty_ShouldFail()
    {
        var request = new AssignDocumentToTenantRequest("", Guid.NewGuid(), "Acme Corp");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "FileName" &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Validate_WhenFileNameExceedsMaxLength_ShouldFail()
    {
        var longFileName = new string('a', 501);
        var request = new AssignDocumentToTenantRequest(longFileName, Guid.NewGuid(), "Acme Corp");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "FileName" &&
            e.ErrorMessage.Contains("500"));
    }

    [Fact]
    public void Validate_WhenFileNameIsExactlyMaxLength_ShouldSucceed()
    {
        var maxFileName = new string('a', 500);
        var request = new AssignDocumentToTenantRequest(maxFileName, Guid.NewGuid(), "Acme Corp");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region TenantId Validation

    [Fact]
    public void Validate_WhenTenantIdIsEmpty_ShouldFail()
    {
        var request = new AssignDocumentToTenantRequest("document.pdf", Guid.Empty, "Acme Corp");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "TenantId" &&
            e.ErrorMessage.Contains("required"));
    }

    #endregion

    #region TenantName Validation

    [Fact]
    public void Validate_WhenTenantNameIsEmpty_ShouldFail()
    {
        var request = new AssignDocumentToTenantRequest("document.pdf", Guid.NewGuid(), "");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "TenantName" &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Validate_WhenTenantNameExceedsMaxLength_ShouldFail()
    {
        var longName = new string('a', 201);
        var request = new AssignDocumentToTenantRequest("document.pdf", Guid.NewGuid(), longName);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "TenantName" &&
            e.ErrorMessage.Contains("200"));
    }

    #endregion
}
