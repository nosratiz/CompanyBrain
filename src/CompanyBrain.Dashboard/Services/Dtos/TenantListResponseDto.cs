namespace CompanyBrain.Dashboard.Services.Dtos;

public sealed record TenantListResponseDto(IReadOnlyList<TenantSummaryDto> Tenants);
