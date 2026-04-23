using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Search.Vector;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Tests.Services;

public sealed class DeepRootSettingsServiceTests : IDisposable
{
    private readonly DbContextOptions<DocumentAssignmentDbContext> _options;
    private readonly TestDbContextFactory _factory;
    private readonly EphemeralDataProtectionProvider _dataProtection;
    private readonly DeepRootSettingsService _sut;

    public DeepRootSettingsServiceTests()
    {
        _options = new DbContextOptionsBuilder<DocumentAssignmentDbContext>()
            .UseInMemoryDatabase($"DeepRootSettings_{Guid.NewGuid():N}")
            .Options;
        _factory = new TestDbContextFactory(_options);
        _dataProtection = new EphemeralDataProtectionProvider();
        _sut = new DeepRootSettingsService(
            _factory,
            _dataProtection,
            NullLogger<DeepRootSettingsService>.Instance);
    }

    [Fact]
    public async Task Default_settings_resolve_to_provider_None()
    {
        var options = await _sut.GetEmbeddingOptionsAsync();
        options.Provider.Should().Be(EmbeddingProviderType.None);
        options.ApiKey.Should().BeNull();
    }

    [Fact]
    public async Task Saved_api_key_round_trips_through_data_protection_and_never_persists_plaintext()
    {
        const string secret = "sk-very-secret-voyage-key-123";

        var saved = await _sut.UpdateAsync(
            provider: "Voyage",
            model: "voyage-3-large",
            dimensions: 2048,
            apiKey: secret,
            endpoint: string.Empty,
            databasePath: string.Empty);

        saved.EncryptedApiKey.Should().NotBeNullOrEmpty();
        saved.EncryptedApiKey.Should().NotContain(secret, "the API key must never be persisted in plaintext");

        var resolved = await _sut.GetEmbeddingOptionsAsync();
        resolved.Provider.Should().Be(EmbeddingProviderType.Voyage);
        resolved.Model.Should().Be("voyage-3-large");
        resolved.Dimensions.Should().Be(2048);
        resolved.ApiKey.Should().Be(secret);
    }

    [Fact]
    public async Task Null_api_key_on_update_preserves_existing_encrypted_key()
    {
        const string secret = "sk-keep-me";
        await _sut.UpdateAsync("OpenAI", "text-embedding-3-small", 1536, secret, string.Empty, string.Empty);

        var beforeRow = await _sut.GetSettingsAsync();
        var beforeCipher = beforeRow.EncryptedApiKey;

        await _sut.UpdateAsync("OpenAI", "text-embedding-3-large", 3072, apiKey: null, string.Empty, string.Empty);

        var afterRow = await _sut.GetSettingsAsync();
        afterRow.EncryptedApiKey.Should().Be(beforeCipher);
        afterRow.Model.Should().Be("text-embedding-3-large");
        afterRow.Dimensions.Should().Be(3072);

        var resolved = await _sut.GetEmbeddingOptionsAsync();
        resolved.ApiKey.Should().Be(secret);
    }

    [Fact]
    public async Task Empty_api_key_on_update_clears_stored_key()
    {
        await _sut.UpdateAsync("Gemini", "text-embedding-004", 768, "g-key", string.Empty, string.Empty);

        await _sut.UpdateAsync("Gemini", "text-embedding-004", 768, apiKey: string.Empty, string.Empty, string.Empty);

        var resolved = await _sut.GetEmbeddingOptionsAsync();
        resolved.ApiKey.Should().BeNull();
    }

    public void Dispose()
    {
        // EphemeralDataProtectionProvider is in-memory; nothing to clean up.
    }

    private sealed class TestDbContextFactory(DbContextOptions<DocumentAssignmentDbContext> options)
        : IDbContextFactory<DocumentAssignmentDbContext>
    {
        public DocumentAssignmentDbContext CreateDbContext() => new(options);
    }
}
