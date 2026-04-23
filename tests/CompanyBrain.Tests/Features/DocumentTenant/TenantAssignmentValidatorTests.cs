using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using CompanyBrain.Dashboard.Features.DocumentTenant.Validators;
using FluentAssertions;

namespace CompanyBrain.Tests.Features.DocumentTenant;

public sealed class TenantAssignmentValidatorTests
{
    private readonly TenantAssignmentValidator _validator = new();

    #region Valid Assignments

    [Fact]
    public void Validate_WithValidAssignment_ShouldSucceed()
    {
        var assignment = new TenantAssignment(Guid.NewGuid(), "Acme Corp");

        var result = _validator.Validate(assignment);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region TenantId Validation

    [Fact]
    public void Validate_WhenTenantIdIsEmpty_ShouldFail()
    {
        var assignment = new TenantAssignment(Guid.Empty, "Acme Corp");

        var result = _validator.Validate(assignment);

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
        var assignment = new TenantAssignment(Guid.NewGuid(), "");

        var result = _validator.Validate(assignment);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "TenantName" &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Validate_WhenTenantNameExceedsMaxLength_ShouldFail()
    {
        var longName = new string('a', 201);
        var assignment = new TenantAssignment(Guid.NewGuid(), longName);

        var result = _validator.Validate(assignment);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "TenantName" &&
            e.ErrorMessage.Contains("200"));
    }

    [Fact]
    public void Validate_WhenTenantNameIsExactlyMaxLength_ShouldSucceed()
    {
        var maxName = new string('a', 200);
        var assignment = new TenantAssignment(Guid.NewGuid(), maxName);

        var result = _validator.Validate(assignment);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
