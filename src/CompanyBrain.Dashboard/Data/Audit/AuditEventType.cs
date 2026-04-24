namespace CompanyBrain.Dashboard.Data.Audit;

public enum AuditEventType
{
    DocumentAccessed,
    DocumentCreated,
    DocumentDeleted,
    DocumentUpdated,
    CollectionAccessed,
    CustomToolExecuted,
    SyncScheduleRun,
    SearchPerformed,
    TenantAssigned,
    SettingsChanged,
    LoginSucceeded,
    LoginFailed,
}
