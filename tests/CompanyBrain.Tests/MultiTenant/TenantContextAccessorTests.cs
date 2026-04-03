using CompanyBrain.MultiTenant.Abstractions;
using CompanyBrain.MultiTenant.Services;
using FluentAssertions;

namespace CompanyBrain.Tests.MultiTenant;

public sealed class TenantContextAccessorTests
{
    [Fact]
    public void SetTenant_ShouldSetTenantContext()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        var tenantId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        // Act
        accessor.SetTenant(tenantId, tenantSlug);

        // Assert
        accessor.TenantId.HasValue.Should().BeTrue();
        accessor.TenantId.Should().Be(tenantId);
        accessor.TenantSlug.Should().Be(tenantSlug);
    }

    [Fact]
    public void HasTenant_WhenNotSet_ShouldReturnFalse()
    {
        // Arrange
        var accessor = new TenantContextAccessor();

        // Assert
        accessor.TenantId.HasValue.Should().BeFalse();
        accessor.TenantId.Should().BeNull();
        accessor.TenantSlug.Should().BeNull();
    }

    [Fact]
    public void Clear_ShouldRemoveTenantContext()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(Guid.NewGuid(), "test");

        // Act
        accessor.Clear();

        // Assert
        accessor.TenantId.HasValue.Should().BeFalse();
        accessor.TenantId.Should().BeNull();
        accessor.TenantSlug.Should().BeNull();
    }

    [Fact]
    public async Task TenantContext_ShouldBeIsolatedPerAsyncFlow()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Act - Simulate two concurrent requests
        var task1 = Task.Run(async () =>
        {
            accessor.SetTenant(tenant1Id, "tenant-1");
            await Task.Delay(50); // Simulate work
            return accessor.TenantId;
        });

        var task2 = Task.Run(async () =>
        {
            accessor.SetTenant(tenant2Id, "tenant-2");
            await Task.Delay(50); // Simulate work
            return accessor.TenantId;
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert - Each task should see its own tenant
        results.Should().Contain(tenant1Id);
        results.Should().Contain(tenant2Id);
    }

    [Fact]
    public async Task TenantContext_ShouldFlowThroughAsyncCalls()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        var tenantId = Guid.NewGuid();
        var slug = "test-tenant";

        // Act
        accessor.SetTenant(tenantId, slug);

        var capturedId = await CaptureIdAsync(accessor);
        var capturedSlug = await CaptureSlugAsync(accessor);

        // Assert
        capturedId.Should().Be(tenantId);
        capturedSlug.Should().Be(slug);
    }

    private static async Task<Guid?> CaptureIdAsync(ITenantContext context)
    {
        await Task.Delay(10);
        return context.TenantId;
    }

    private static async Task<string?> CaptureSlugAsync(ITenantContext context)
    {
        await Task.Delay(10);
        return context.TenantSlug;
    }
}
