using CompanyBrain.MultiTenant.Domain;
using FluentAssertions;

namespace CompanyBrain.Tests.MultiTenant;

public sealed class ApiKeyDomainTests
{
    #region Generate Tests

    [Fact]
    public void Generate_ShouldCreateKeyWithCorrectPrefix()
    {
        // Act
        var (plainKey, entity) = ApiKey.Generate(Guid.NewGuid(), "Test Key");

        // Assert
        plainKey.Should().StartWith("cb_");
        entity.KeyPrefix.Should().StartWith("cb_");
        entity.KeyPrefix.Should().HaveLength(11); // cb_ + 8 chars
    }

    [Fact]
    public void Generate_ShouldCreateUniqueKeys()
    {
        // Act
        var keys = Enumerable.Range(0, 100)
            .Select(_ => ApiKey.Generate(Guid.NewGuid(), "Test"))
            .Select(x => x.PlainKey)
            .ToList();

        // Assert
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_ShouldHashKeySecurely()
    {
        // Act
        var (plainKey, entity) = ApiKey.Generate(Guid.NewGuid(), "Test Key");

        // Assert
        entity.KeyHash.Should().NotBe(plainKey);
        entity.KeyHash.Should().NotContain(plainKey);
        entity.KeyHash.Should().HaveLength(44); // Base64 SHA256
    }

    [Fact]
    public void Generate_ShouldSetProvidedScope()
    {
        // Act
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Admin Key", ApiKeyScope.Admin);

        // Assert
        entity.Scope.Should().Be(ApiKeyScope.Admin);
    }

    [Fact]
    public void Generate_ShouldSetProvidedExpiry()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Expiring Key", expiresAt: expiresAt);

        // Assert
        entity.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Generate_PlainKeyShouldBeUrlSafe()
    {
        // Act
        var (plainKey, _) = ApiKey.Generate(Guid.NewGuid(), "Test");

        // Assert - Should not contain characters that need URL encoding
        plainKey.Should().NotContain("+");
        plainKey.Should().NotContain("/");
        plainKey.Should().NotContain("=");
    }

    #endregion

    #region HashKey Tests

    [Fact]
    public void HashKey_ShouldProduceSameHashForSameInput()
    {
        // Arrange
        var plainKey = "cb_test_key_12345";

        // Act
        var hash1 = ApiKey.HashKey(plainKey);
        var hash2 = ApiKey.HashKey(plainKey);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashKey_ShouldProduceDifferentHashForDifferentInput()
    {
        // Act
        var hash1 = ApiKey.HashKey("cb_key_one");
        var hash2 = ApiKey.HashKey("cb_key_two");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashKey_GeneratedKeyShouldMatchStoredHash()
    {
        // Arrange
        var (plainKey, entity) = ApiKey.Generate(Guid.NewGuid(), "Test");

        // Act
        var computedHash = ApiKey.HashKey(plainKey);

        // Assert
        computedHash.Should().Be(entity.KeyHash);
    }

    #endregion

    #region IsValid Tests

    [Fact]
    public void IsValid_WhenNotRevokedAndNotExpired_ShouldReturnTrue()
    {
        // Arrange
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Test");

        // Assert
        entity.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenRevoked_ShouldReturnFalse()
    {
        // Arrange
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Test");
        entity.IsRevoked = true;

        // Assert
        entity.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var (_, entity) = ApiKey.Generate(
            Guid.NewGuid(), "Expired",
            expiresAt: DateTime.UtcNow.AddDays(-1));

        // Assert
        entity.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpiresInFuture_ShouldReturnTrue()
    {
        // Arrange
        var (_, entity) = ApiKey.Generate(
            Guid.NewGuid(), "Future",
            expiresAt: DateTime.UtcNow.AddDays(30));

        // Assert
        entity.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenNoExpiry_ShouldReturnTrue()
    {
        // Arrange
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "No Expiry");

        // Assert - No expiry means never expires
        entity.ExpiresAt.Should().BeNull();
        entity.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenRevokedAndExpired_ShouldReturnFalse()
    {
        // Arrange
        var (_, entity) = ApiKey.Generate(
            Guid.NewGuid(), "Bad Key",
            expiresAt: DateTime.UtcNow.AddDays(-1));
        entity.IsRevoked = true;

        // Assert
        entity.IsValid().Should().BeFalse();
    }

    #endregion

    #region ApiKeyScope Tests

    [Theory]
    [InlineData(ApiKeyScope.ReadOnly)]
    [InlineData(ApiKeyScope.WriteDocuments)]
    [InlineData(ApiKeyScope.ManageResources)]
    [InlineData(ApiKeyScope.Admin)]
    public void Generate_ShouldSupportAllScopes(ApiKeyScope scope)
    {
        // Act
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Test", scope);

        // Assert
        entity.Scope.Should().Be(scope);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void Generate_ShouldSetDefaultRateLimits()
    {
        // Act
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Test");

        // Assert
        entity.RequestsPerMinute.Should().Be(60);
        entity.RequestsPerDay.Should().Be(10_000);
    }

    [Fact]
    public void Generate_ShouldNotBeRevokedByDefault()
    {
        // Act
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Test");

        // Assert
        entity.IsRevoked.Should().BeFalse();
        entity.RevokedReason.Should().BeNull();
    }

    [Fact]
    public void Generate_ShouldSetCreatedAtToNow()
    {
        // Act
        var before = DateTime.UtcNow;
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Test");
        var after = DateTime.UtcNow;

        // Assert
        entity.CreatedAt.Should().BeOnOrAfter(before);
        entity.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Generate_LastUsedAtShouldBeNull()
    {
        // Act
        var (_, entity) = ApiKey.Generate(Guid.NewGuid(), "Test");

        // Assert
        entity.LastUsedAt.Should().BeNull();
    }

    #endregion
}
