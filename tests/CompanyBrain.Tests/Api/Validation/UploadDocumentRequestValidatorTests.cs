using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Api.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace CompanyBrain.Tests.Api.Validation;

public sealed class UploadDocumentRequestValidatorTests
{
    private readonly UploadDocumentRequestValidator _validator = new();

    private static IFormFile CreateMockFile(long length = 1024)
    {
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(length);
        return file;
    }

    #region Valid Requests

    [Fact]
    public void Validate_WithValidFileAndNoName_ShouldSucceed()
    {
        var request = new UploadDocumentRequest(CreateMockFile(), null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidFileAndName_ShouldSucceed()
    {
        var request = new UploadDocumentRequest(CreateMockFile(), "my-document.pdf");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region File Validation

    [Fact]
    public void Validate_WhenFileIsNull_ShouldFail()
    {
        var request = new UploadDocumentRequest(null, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "File");
    }

    [Fact]
    public void Validate_WhenFileIsEmpty_ShouldFail()
    {
        var request = new UploadDocumentRequest(CreateMockFile(0), null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "File.Length" &&
            e.ErrorMessage.Contains("empty"));
    }

    #endregion

    #region Name Validation

    [Fact]
    public void Validate_WhenNameExceedsMaxLength_ShouldFail()
    {
        var longName = new string('a', 201);
        var request = new UploadDocumentRequest(CreateMockFile(), longName);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhenNameIsNull_ShouldSkipNameValidation()
    {
        var request = new UploadDocumentRequest(CreateMockFile(), null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenNameIsWhitespace_ShouldSkipNameValidation()
    {
        var request = new UploadDocumentRequest(CreateMockFile(), "   ");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    #endregion
}
