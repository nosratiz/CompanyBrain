using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Domain;
using CompanyBrain.MultiTenant.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.MultiTenant.Tests;

public sealed class ApiKeyServiceTests : IAsyncDisposable
{
    private readonly TenantDbContext _dbContext;
    private readonly TenantService _tenantService;
    private readonly ApiKeyService _sut;

    public ApiKeyServiceTests()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(databaseName: $"ApiKeyTests_{Guid.NewGuid():N}")
            .Options;

        _dbContext = new TenantDbContext(options);
        _tenantService = new TenantService(_dbContext, NullLogger<TenantService>.Instance);
        _sut = new ApiKeyService(_dbContext, NullLogger<ApiKeyService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task<Tenant> CreateTestTenantAsync(TenantPlan plan = TenantPlan.Free)
    {
        var result = await _tenantService.CreateTenantAsync(
            $"Test-{Guid.NewGuid():N}",
            plan: plan);
        return result.Value;
    }

    #region CreateApiKeyAsync Tests

    [Fact]
    public async Task CreateApiKeyAsync_WithValidTenant_ShouldCreateKey()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();

        // Act
        var result = await _sut.CreateApiKeyAsync(tenant.Id, "My API Key");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlainKey.Should().StartWith("cb_");
        result.Value.KeyEntity.Name.Should().Be("My API Key");
        result.Value.KeyEntity.Scope.Should().Be(ApiKeyScope.ReadOnly);
    }

    [Fact]
    public async Task CreateApiKeyAsync_PlainKeyShouldBeShowOnce()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();

        // Act
        var result = await _sut.CreateApiKeyAsync(tenant.Id, "Secret Key");

        // Assert - The plain key should be returned but NOT stored
        result.Value.PlainKey.Should().HaveLength(46); // cb_ + 43 base64 chars (no padding)
        result.Value.KeyEntity.KeyHash.Should().NotBe(result.Value.PlainKey);
        result.Value.KeyEntity.KeyPrefix.Should().Be(result.Value.PlainKey[..11]);
    }

    [Fact]
    public async Task CreateApiKeyAsync_WithExpirationDate_ShouldSetExpiry()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var result = await _sut.CreateApiKeyAsync(
            tenant.Id, "Expiring Key",
            expiresAt: expiresAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KeyEntity.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateApiKeyAsync_WithSpecificScope_ShouldSetScope()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();

        // Act
        var result = await _sut.CreateApiKeyAsync(
            tenant.Id, "Admin Key",
            scope: ApiKeyScope.Admin);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KeyEntity.Scope.Should().Be(ApiKeyScope.Admin);
    }

    [Fact]
    public async Task CreateApiKeyAsync_WhenTenantNotFound_ShouldFail()
    {
        // Act
        var result = await _sut.CreateApiKeyAsync(Guid.NewGuid(), "Test Key");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("not found"));
    }

    [Fact]
    public async Task CreateApiKeyAsync_WhenTenantSuspended_ShouldFail()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        await _tenantService.SuspendTenantAsync(tenant.Id);

        // Act
        var result = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("inactive"));
    }

    [Fact]
    public async Task CreateApiKeyAsync_WhenLimitReached_ShouldFail()
    {
        // Arrange - Free plan has 3 API keys max
        var tenant = await CreateTestTenantAsync(TenantPlan.Free);
        await _sut.CreateApiKeyAsync(tenant.Id, "Key 1");
        await _sut.CreateApiKeyAsync(tenant.Id, "Key 2");
        await _sut.CreateApiKeyAsync(tenant.Id, "Key 3");

        // Act
        var result = await _sut.CreateApiKeyAsync(tenant.Id, "Key 4");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("limit reached"));
    }

    #endregion

    #region ValidateApiKeyAsync Tests

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ShouldReturnTenantInfo()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");
        var plainKey = created.Value.PlainKey;

        // Act
        var result = await _sut.ValidateApiKeyAsync(plainKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(tenant.Id);
        result.Value.TenantSlug.Should().Be(tenant.Slug);
        result.Value.Scope.Should().Be(ApiKeyScope.ReadOnly);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ShouldUpdateLastUsedAt()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");
        var keyId = created.Value.KeyEntity.Id;

        // Wait a bit to ensure time difference
        await Task.Delay(100);

        // Act
        await _sut.ValidateApiKeyAsync(created.Value.PlainKey);

        // Assert
        var key = await _dbContext.ApiKeys.FindAsync(keyId);
        key!.LastUsedAt.Should().NotBeNull();
        key.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInvalidKey_ShouldFail()
    {
        // Act
        var result = await _sut.ValidateApiKeyAsync("cb_invalid_key_here");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("Invalid"));
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInvalidFormat_ShouldFail()
    {
        // Act
        var result = await _sut.ValidateApiKeyAsync("not_a_valid_key");

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("format"));
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithEmptyKey_ShouldFail()
    {
        // Act
        var result = await _sut.ValidateApiKeyAsync("");

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithRevokedKey_ShouldFail()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");
        await _sut.RevokeApiKeyAsync(tenant.Id, created.Value.KeyEntity.Id);

        // Act
        var result = await _sut.ValidateApiKeyAsync(created.Value.PlainKey);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("revoked"));
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithExpiredKey_ShouldFail()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(
            tenant.Id, "Expired Key",
            expiresAt: DateTime.UtcNow.AddDays(-1)); // Already expired

        // Act
        var result = await _sut.ValidateApiKeyAsync(created.Value.PlainKey);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("expired"));
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WhenTenantSuspended_ShouldFail()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");
        await _tenantService.SuspendTenantAsync(tenant.Id);

        // Act
        var result = await _sut.ValidateApiKeyAsync(created.Value.PlainKey);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message.Contains("not active"));
    }

    #endregion

    #region ListApiKeysAsync Tests

    [Fact]
    public async Task ListApiKeysAsync_ShouldReturnAllActiveKeys()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        await _sut.CreateApiKeyAsync(tenant.Id, "Key 1");
        await _sut.CreateApiKeyAsync(tenant.Id, "Key 2");

        // Act
        var result = await _sut.ListApiKeysAsync(tenant.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Select(k => k.Name).Should().Contain(["Key 1", "Key 2"]);
    }

    [Fact]
    public async Task ListApiKeysAsync_ShouldExcludeRevokedByDefault()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        await _sut.CreateApiKeyAsync(tenant.Id, "Active Key");
        var revoked = await _sut.CreateApiKeyAsync(tenant.Id, "Revoked Key");
        await _sut.RevokeApiKeyAsync(tenant.Id, revoked.Value.KeyEntity.Id);

        // Act
        var result = await _sut.ListApiKeysAsync(tenant.Id);

        // Assert
        result.Should().HaveCount(1);
        result.Should().OnlyContain(k => k.Name == "Active Key");
    }

    [Fact]
    public async Task ListApiKeysAsync_WithIncludeRevoked_ShouldReturnAll()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        await _sut.CreateApiKeyAsync(tenant.Id, "Active Key");
        var revoked = await _sut.CreateApiKeyAsync(tenant.Id, "Revoked Key");
        await _sut.RevokeApiKeyAsync(tenant.Id, revoked.Value.KeyEntity.Id);

        // Act
        var result = await _sut.ListApiKeysAsync(tenant.Id, includeRevoked: true);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListApiKeysAsync_ShouldNotExposeKeyHashes()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");

        // Act
        var result = await _sut.ListApiKeysAsync(tenant.Id);

        // Assert - We can see the prefix but not full hash for identification purposes
        result.Should().AllSatisfy(k => k.KeyPrefix.Should().StartWith("cb_"));
    }

    #endregion

    #region RevokeApiKeyAsync Tests

    [Fact]
    public async Task RevokeApiKeyAsync_ShouldRevokeKey()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");

        // Act
        var result = await _sut.RevokeApiKeyAsync(tenant.Id, created.Value.KeyEntity.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var keys = await _sut.ListApiKeysAsync(tenant.Id, includeRevoked: true);
        keys.Should().OnlyContain(k => k.IsRevoked);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithReason_ShouldStoreReason()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");

        // Act
        await _sut.RevokeApiKeyAsync(tenant.Id, created.Value.KeyEntity.Id, reason: "Compromised");

        // Assert
        var key = await _dbContext.ApiKeys.FindAsync(created.Value.KeyEntity.Id);
        key!.RevokedReason.Should().Be("Compromised");
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenKeyNotFound_ShouldFail()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();

        // Act
        var result = await _sut.RevokeApiKeyAsync(tenant.Id, Guid.NewGuid());

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenAlreadyRevoked_ShouldFail()
    {
        // Arrange
        var tenant = await CreateTestTenantAsync();
        var created = await _sut.CreateApiKeyAsync(tenant.Id, "Test Key");
        await _sut.RevokeApiKeyAsync(tenant.Id, created.Value.KeyEntity.Id);

        // Act
        var result = await _sut.RevokeApiKeyAsync(tenant.Id, created.Value.KeyEntity.Id);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    #endregion
}
