using System.Text.Json;
using CompanyBrain.Dashboard.Data.Audit;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Services.Audit;

public sealed class AuditService(
    IDbContextFactory<AuditDbContext> contextFactory,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task LogAsync(AuditEventType eventType, AuditEntry entry)
    {
        try
        {
            var log = new AuditLog
            {
                EventType = eventType,
                ActorId = entry.ActorId,
                ActorEmail = entry.ActorEmail,
                TenantId = entry.TenantId,
                ResourceType = entry.ResourceType,
                ResourceId = entry.ResourceId,
                ResourceName = entry.ResourceName,
                Metadata = entry.Metadata is not null
                    ? JsonSerializer.Serialize(entry.Metadata)
                    : null,
                Success = entry.Success,
                ErrorMessage = entry.ErrorMessage,
                Timestamp = DateTime.UtcNow,
                IpAddress = entry.IpAddress,
            };

            await using var context = await contextFactory.CreateDbContextAsync();
            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log write failed for event {EventType} — continuing", eventType);
        }
    }
}
