namespace CompanyBrain.Dashboard.Api.Contracts;

internal sealed record UploadDocumentRequest(IFormFile? File, string? Name);
