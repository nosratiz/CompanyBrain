using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using CompanyBrain.Dashboard.Features.DocumentTenant.Validators;
using FluentAssertions;

namespace CompanyBrain.Tests.Features.DocumentTenant;

public sealed class UpdateDocumentTenantsRequestValidatorTests
{
    private readonly UpdateDocumentTenantsRequestValidator _validator = new();

    #region Valid Requests

    [Fact]
    public void Validate_WithValidRequest_ShouldSucceed()
    {
        var request = new UpdateDocumentTenantsRequest(
            "document.pdf",
            [new TenantAssignment(Guid.NewGuid(), "Acme Corp")]);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyTenantsList_ShouldSucceed()
    {
        var request = new UpdateDocumentTenantsRequest("document.pdf", []);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region FileName Validation

    [Fact]
    public void Validate_WhenFileNameIsEmpty_ShouldFail()
    {
        var request = new UpdateDocumentTenantsRequest("", []);

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
        var request = new UpdateDocumentTenantsRequest(longFileName, []);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "FileName" &&
            e.ErrorMessage.Contains("500"));
    }

    #endregion

    #region Tenants Validation

    [Fact]
    public void Validate_WhenTenantsIsNull_ShouldFail()
    {
        var request = new UpdateDocumentTenantsRequest("document.pdf", null!);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Tenants" &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Validate_WhenTenantAssignmentIsInvalid_ShouldFail()
    {
        var request = new UpdateDocumentTenantsRequest(
            "document.pdf",
            [new TenantAssignment(Guid.Empty, "")]);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("TenantId"));
    }

    [Fact]
    public void Validate_WithMultipleValidTenants_ShouldSucceed()
    {
        var request = new UpdateDocumentTenantsRequest(
            "document.pdf",
        [
            new TenantAssignment(Guid.NewGuid(), "Tenant A"),
            new TenantAssignment(Guid.NewGuid(), "Tenant B"),
            new TenantAssignment(Guid.NewGuid(), "Tenant C")
        ]);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
