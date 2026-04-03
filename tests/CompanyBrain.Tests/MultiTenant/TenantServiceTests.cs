using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Domain;
using CompanyBrain.MultiTenant.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Tests.MultiTenant;

public sealed class TenantServiceTests : IAsyncDisposable
{
    private readonly TenantDbContext _dbContext;
    private readonly TenantService _sut;

    public TenantServiceTests()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantTests_{Guid.NewGuid():N}")
            .Options;

        _dbContext = new TenantDbContext(options);
        _sut = new TenantService(_dbContext, NullLogger<TenantService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region CreateTenantAsync Tests

    [Fact]
    public async Task CreateTenantAsync_WithValidName_ShouldCreateTenant()
    {
        // Act
        var result = await _sut.CreateTenantAsync("Acme Corporation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Acme Corporation");
        result.Value.Slug.Should().Be("acme-corporation");
        result.Value.Status.Should().Be(TenantStatus.Active);
        result.Value.Plan.Should().Be(TenantPlan.Free);
    }

    [Fact]
    public async Task CreateTenantAsync_WithDescription_ShouldSetDescription()
    {
        // Act
        var result = await _sut.CreateTenantAsync(
            "Test Corp",
            description: "A test corporation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("A test corporation");
    }

    [Fact]
    public async Task CreateTenantAsync_WithSpecificPlan_ShouldSetPlanLimits()
    {
        // Act
        var result = await _sut.CreateTenantAsync(
            "Enterprise Corp",
            plan: TenantPlan.Enterprise);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Plan.Should().Be(TenantPlan.Enterprise);
        result.Value.MaxDocuments.Should().Be(50_000);
        result.Value.MaxApiKeys.Should().Be(100);
        result.Value.MaxStorageBytes.Should().Be(100L * 1024 * 1024 * 1024); // 100 GB
    }

    [Fact]
    public async Task CreateTenantAsync_WithDuplicateSlug_ShouldFail()
    {
        // Arrange
        await _sut.CreateTenantAsync("Test Company");

        // Act
        var result = await _sut.CreateTenantAsync("Test Company");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("already exists"));
    }

    [Theory]
    [InlineData("My Company!", "my-company")]
    [InlineData("Test   Multiple   Spaces", "test-multiple-spaces")]
    [InlineData("Special@#$%Characters", "specialcharacters")]
    [InlineData("  Leading Trailing  ", "leading-trailing")]
    public async Task CreateTenantAsync_ShouldGenerateCorrectSlug(string name, string expectedSlug)
    {
        // Act
        var result = await _sut.CreateTenantAsync(name);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be(expectedSlug);
    }

    [Theory]
    [InlineData(TenantPlan.Free, 100, 3)]
    [InlineData(TenantPlan.Starter, 500, 10)]
    [InlineData(TenantPlan.Professional, 2_000, 25)]
    [InlineData(TenantPlan.Enterprise, 50_000, 100)]
    public async Task CreateTenantAsync_ShouldSetCorrectLimitsForPlan(
        TenantPlan plan, int expectedDocs, int expectedKeys)
    {
        // Act
        var result = await _sut.CreateTenantAsync($"Tenant-{plan}", plan: plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MaxDocuments.Should().Be(expectedDocs);
        result.Value.MaxApiKeys.Should().Be(expectedKeys);
    }

    #endregion

    #region GetTenantAsync Tests

    [Fact]
    public async Task GetTenantAsync_WhenTenantExists_ShouldReturnTenant()
    {
        // Arrange
        var created = await _sut.CreateTenantAsync("Test Company");
        var tenantId = created.Value.Id;

        // Act
        var result = await _sut.GetTenantAsync(tenantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Company");
    }

    [Fact]
    public async Task GetTenantAsync_WhenTenantDoesNotExist_ShouldFail()
    {
        // Act
        var result = await _sut.GetTenantAsync(Guid.NewGuid());

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("not found"));
    }

    #endregion

    #region GetTenantBySlugAsync Tests

    [Fact]
    public async Task GetTenantBySlugAsync_WhenTenantExists_ShouldReturnTenant()
    {
        // Arrange
        await _sut.CreateTenantAsync("Test Company");

        // Act
        var result = await _sut.GetTenantBySlugAsync("test-company");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("test-company");
    }

    [Fact]
    public async Task GetTenantBySlugAsync_WhenTenantDoesNotExist_ShouldFail()
    {
        // Act
        var result = await _sut.GetTenantBySlugAsync("nonexistent");

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    #endregion

    #region ListTenantsAsync Tests

    [Fact]
    public async Task ListTenantsAsync_ShouldReturnAllActiveTenants()
    {
        // Arrange
        await _sut.CreateTenantAsync("Company A");
        await _sut.CreateTenantAsync("Company B");
        await _sut.CreateTenantAsync("Company C");

        // Act
        var result = await _sut.ListTenantsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(t => t.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListTenantsAsync_ShouldExcludeDeletedTenants()
    {
        // Arrange
        await _sut.CreateTenantAsync("Active Company");
        var deleted = (await _sut.CreateTenantAsync("Deleted Company")).Value;

        // Manually mark as deleted
        deleted.Status = TenantStatus.Deleted;
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ListTenantsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().OnlyContain(t => t.Name == "Active Company");
    }

    #endregion

    #region UpdatePlanAsync Tests

    [Fact]
    public async Task UpdatePlanAsync_ShouldUpdatePlanAndLimits()
    {
        // Arrange
        var created = await _sut.CreateTenantAsync("Test Company", plan: TenantPlan.Free);

        // Act
        var result = await _sut.UpdatePlanAsync(created.Value.Id, TenantPlan.Professional);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Plan.Should().Be(TenantPlan.Professional);
        result.Value.MaxDocuments.Should().Be(2_000);
        result.Value.MaxApiKeys.Should().Be(25);
        result.Value.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePlanAsync_WhenTenantNotFound_ShouldFail()
    {
        // Act
        var result = await _sut.UpdatePlanAsync(Guid.NewGuid(), TenantPlan.Enterprise);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    #endregion

    #region SuspendTenantAsync Tests

    [Fact]
    public async Task SuspendTenantAsync_ShouldSetStatusToSuspended()
    {
        // Arrange
        var created = await _sut.CreateTenantAsync("Test Company");

        // Act
        var result = await _sut.SuspendTenantAsync(created.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var tenant = await _sut.GetTenantAsync(created.Value.Id);
        tenant.Value.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public async Task SuspendTenantAsync_WhenTenantNotFound_ShouldFail()
    {
        // Act
        var result = await _sut.SuspendTenantAsync(Guid.NewGuid());

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    #endregion
}
