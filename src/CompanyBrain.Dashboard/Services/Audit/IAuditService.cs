using CompanyBrain.Dashboard.Data.Audit;

namespace CompanyBrain.Dashboard.Services.Audit;

public interface IAuditService
{
    Task LogAsync(AuditEventType eventType, AuditEntry entry);
}
