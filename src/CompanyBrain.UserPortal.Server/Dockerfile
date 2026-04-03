# CompanyBrain UserPortal Server Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/CompanyBrain.Core/CompanyBrain.Core.csproj src/CompanyBrain.Core/
COPY src/CompanyBrain.MultiTenant/CompanyBrain.MultiTenant.csproj src/CompanyBrain.MultiTenant/
COPY src/CompanyBrain.UserPortal/CompanyBrain.UserPortal.csproj src/CompanyBrain.UserPortal/
COPY src/CompanyBrain.UserPortal.Server/CompanyBrain.UserPortal.Server.csproj src/CompanyBrain.UserPortal.Server/

# Restore dependencies
RUN dotnet restore src/CompanyBrain.UserPortal.Server/CompanyBrain.UserPortal.Server.csproj

# Copy source code
COPY src/CompanyBrain.Core/ src/CompanyBrain.Core/
COPY src/CompanyBrain.MultiTenant/ src/CompanyBrain.MultiTenant/
COPY src/CompanyBrain.UserPortal/ src/CompanyBrain.UserPortal/
COPY src/CompanyBrain.UserPortal.Server/ src/CompanyBrain.UserPortal.Server/

# Build and publish
RUN dotnet publish src/CompanyBrain.UserPortal.Server/CompanyBrain.UserPortal.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Create data directories
RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "CompanyBrain.UserPortal.Server.dll"]
