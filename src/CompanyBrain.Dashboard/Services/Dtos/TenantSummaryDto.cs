namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record TenantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    int Status,
    int Plan,
    DateTime CreatedAt)
{
    public string StatusName => Status switch
    {
        0 => "Pending",
        1 => "Active",
        2 => "Suspended",
        3 => "Deleted",
        _ => "Unknown"
    };

    public string PlanName => Plan switch
    {
        0 => "Free",
        1 => "Basic",
        2 => "Professional",
        3 => "Enterprise",
        _ => "Unknown"
    };
}
