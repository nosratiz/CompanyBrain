using CompanyBrain.Dashboard.Helpers;
using FluentAssertions;
using MudBlazor;

namespace CompanyBrain.Tests.Helpers;

public sealed class UiDisplayHelperTests
{
    #region GetMimeTypeLabel Tests

    [Theory]
    [InlineData("text/markdown", "Markdown")]
    [InlineData("text/plain", "Text")]
    [InlineData("text/html", "HTML")]
    [InlineData("application/json", "JSON")]
    [InlineData("application/pdf", "PDF")]
    public void GetMimeTypeLabel_WithKnownType_ShouldReturnLabel(string mimeType, string expected)
    {
        UiDisplayHelper.GetMimeTypeLabel(mimeType).Should().Be(expected);
    }

    [Fact]
    public void GetMimeTypeLabel_WithUnknownType_ShouldReturnMimeType()
    {
        UiDisplayHelper.GetMimeTypeLabel("application/octet-stream").Should().Be("application/octet-stream");
    }

    [Fact]
    public void GetMimeTypeLabel_WithNull_ShouldReturnUnknown()
    {
        UiDisplayHelper.GetMimeTypeLabel(null).Should().Be("Unknown");
    }

    #endregion

    #region GetMimeTypeColor Tests

    [Theory]
    [InlineData("text/markdown", Color.Primary)]
    [InlineData("text/plain", Color.Default)]
    [InlineData("text/html", Color.Info)]
    [InlineData("application/json", Color.Secondary)]
    [InlineData("application/pdf", Color.Error)]
    public void GetMimeTypeColor_WithKnownType_ShouldReturnColor(string mimeType, Color expected)
    {
        UiDisplayHelper.GetMimeTypeColor(mimeType).Should().Be(expected);
    }

    [Fact]
    public void GetMimeTypeColor_WithUnknownType_ShouldReturnDefault()
    {
        UiDisplayHelper.GetMimeTypeColor("application/octet-stream").Should().Be(Color.Default);
    }

    [Fact]
    public void GetMimeTypeColor_WithNull_ShouldReturnDefault()
    {
        UiDisplayHelper.GetMimeTypeColor(null).Should().Be(Color.Default);
    }

    #endregion

    #region GetTenantStatusColor Tests

    [Theory]
    [InlineData(0, Color.Default)]  // Pending
    [InlineData(1, Color.Success)]  // Active
    [InlineData(2, Color.Warning)]  // Suspended
    [InlineData(3, Color.Error)]    // Deleted
    [InlineData(99, Color.Default)] // Unknown
    public void GetTenantStatusColor_ShouldReturnCorrectColor(int status, Color expected)
    {
        UiDisplayHelper.GetTenantStatusColor(status).Should().Be(expected);
    }

    #endregion

    #region GetTenantStatusName Tests

    [Theory]
    [InlineData(0, "Pending")]
    [InlineData(1, "Active")]
    [InlineData(2, "Suspended")]
    [InlineData(3, "Deleted")]
    [InlineData(99, "Unknown")]
    public void GetTenantStatusName_ShouldReturnCorrectName(int status, string expected)
    {
        UiDisplayHelper.GetTenantStatusName(status).Should().Be(expected);
    }

    #endregion

    #region GetTenantPlanName Tests

    [Theory]
    [InlineData(0, "Free")]
    [InlineData(1, "Basic")]
    [InlineData(2, "Professional")]
    [InlineData(3, "Enterprise")]
    [InlineData(99, "Unknown")]
    public void GetTenantPlanName_ShouldReturnCorrectName(int plan, string expected)
    {
        UiDisplayHelper.GetTenantPlanName(plan).Should().Be(expected);
    }

    #endregion
}
