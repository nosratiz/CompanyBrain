using CompanyBrain.Dashboard.Features.Auth.Interfaces;
using CompanyBrain.Dashboard.Features.Auth.Services;
using FluentAssertions;
using NSubstitute;

namespace CompanyBrain.Tests.Features.Auth;

public sealed class AuthTokenStoreTests
{
    private readonly IAuthSessionStorage _storage;
    private readonly AuthTokenStore _sut;

    public AuthTokenStoreTests()
    {
        _storage = Substitute.For<IAuthSessionStorage>();
        _sut = new AuthTokenStore(_storage);
    }

    #region IsAuthenticated Tests

    [Fact]
    public void IsAuthenticated_WhenNoToken_ShouldReturnFalse()
    {
        _sut.IsAuthenticated.Should().BeFalse();
        _sut.Token.Should().BeNull();
    }

    #endregion

    #region SetOwnerSessionAsync Tests

    [Fact]
    public async Task SetOwnerSessionAsync_ShouldSetAllProperties()
    {
        await _sut.SetOwnerSessionAsync("token-123", "John Doe", "john@example.com", "Admin");

        _sut.Token.Should().Be("token-123");
        _sut.DisplayName.Should().Be("John Doe");
        _sut.Email.Should().Be("john@example.com");
        _sut.Role.Should().Be("Admin");
        _sut.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task SetOwnerSessionAsync_ShouldPersistSession()
    {
        await _sut.SetOwnerSessionAsync("token-123", "John Doe", "john@example.com", "Admin");

        await _storage.Received(1).SaveSessionAsync(
            Arg.Is<AuthSessionData>(d =>
                d.Token == "token-123" &&
                d.DisplayName == "John Doe" &&
                d.Email == "john@example.com" &&
                d.Role == "Admin"));
    }

    #endregion

    #region SetEmployeeSessionAsync Tests

    [Fact]
    public async Task SetEmployeeSessionAsync_ShouldCombineFirstAndLastName()
    {
        await _sut.SetEmployeeSessionAsync("emp-token", "Jane", "Smith", "jane@example.com");

        _sut.DisplayName.Should().Be("Jane Smith");
        _sut.Email.Should().Be("jane@example.com");
        _sut.Role.Should().Be("Employee");
        _sut.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task SetEmployeeSessionAsync_ShouldSaveSession()
    {
        await _sut.SetEmployeeSessionAsync("emp-token", "Jane", "Smith", "jane@example.com");

        await _storage.Received(1).SaveSessionAsync(Arg.Any<AuthSessionData>());
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_ShouldClearAllProperties()
    {
        await _sut.SetOwnerSessionAsync("token", "Name", "email@test.com", "Admin");

        await _sut.ClearAsync();

        _sut.Token.Should().BeNull();
        _sut.DisplayName.Should().BeNull();
        _sut.Email.Should().BeNull();
        _sut.Role.Should().BeNull();
        _sut.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_ShouldClearSessionStorage()
    {
        await _sut.ClearAsync();

        await _storage.Received(1).ClearSessionAsync();
    }

    #endregion

    #region RestoreSessionAsync Tests

    [Fact]
    public async Task RestoreSessionAsync_WhenSessionExists_ShouldRestoreAndReturnTrue()
    {
        _storage.GetSessionAsync().Returns(
            new AuthSessionData("restored-token", "Restored User", "restored@example.com", "Viewer"));

        var result = await _sut.RestoreSessionAsync();

        result.Should().BeTrue();
        _sut.Token.Should().Be("restored-token");
        _sut.DisplayName.Should().Be("Restored User");
        _sut.Email.Should().Be("restored@example.com");
        _sut.Role.Should().Be("Viewer");
        _sut.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreSessionAsync_WhenNoSession_ShouldReturnFalse()
    {
        _storage.GetSessionAsync().Returns((AuthSessionData?)null);

        var result = await _sut.RestoreSessionAsync();

        result.Should().BeFalse();
        _sut.IsAuthenticated.Should().BeFalse();
    }

    #endregion

    #region Synchronous Methods Tests

    [Fact]
    public void SetOwnerSession_ShouldSetProperties()
    {
        _sut.SetOwnerSession("sync-token", "Sync User", "sync@test.com", "Admin");

        _sut.Token.Should().Be("sync-token");
        _sut.DisplayName.Should().Be("Sync User");
        _sut.Email.Should().Be("sync@test.com");
        _sut.Role.Should().Be("Admin");
        _sut.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void SetEmployeeSession_ShouldCombineNames()
    {
        _sut.SetEmployeeSession("emp-sync-token", "Bob", "Builder", "bob@test.com");

        _sut.DisplayName.Should().Be("Bob Builder");
        _sut.Role.Should().Be("Employee");
    }

    [Fact]
    public void Clear_ShouldClearProperties()
    {
        _sut.SetOwnerSession("token", "Name", "email@test.com", "Admin");
        _sut.Clear();

        _sut.Token.Should().BeNull();
        _sut.IsAuthenticated.Should().BeFalse();
    }

    #endregion
}
