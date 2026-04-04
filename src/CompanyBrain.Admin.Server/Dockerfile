# CompanyBrain Admin Server Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/CompanyBrain.Core/CompanyBrain.Core.csproj src/CompanyBrain.Core/
COPY src/CompanyBrain.MultiTenant/CompanyBrain.MultiTenant.csproj src/CompanyBrain.MultiTenant/
COPY src/CompanyBrain.AdminPanel/CompanyBrain.AdminPanel.csproj src/CompanyBrain.AdminPanel/
COPY src/CompanyBrain.Admin.Server/CompanyBrain.Admin.Server.csproj src/CompanyBrain.Admin.Server/

# Restore dependencies
RUN dotnet restore src/CompanyBrain.Admin.Server/CompanyBrain.Admin.Server.csproj

# Copy source code
COPY src/CompanyBrain.Core/ src/CompanyBrain.Core/
COPY src/CompanyBrain.MultiTenant/ src/CompanyBrain.MultiTenant/
COPY src/CompanyBrain.AdminPanel/ src/CompanyBrain.AdminPanel/
COPY src/CompanyBrain.Admin.Server/ src/CompanyBrain.Admin.Server/

# Build and publish
RUN dotnet publish src/CompanyBrain.Admin.Server/CompanyBrain.Admin.Server.csproj \
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

ENTRYPOINT ["dotnet", "CompanyBrain.Admin.Server.dll"]
