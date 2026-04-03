# CompanyBrain.MultiTenant Dockerfile
# Build: docker build -t companybrain-multitenant:local -f src/CompanyBrain.MultiTenant/Dockerfile .
# Run: docker run -p 8083:8080 companybrain-multitenant:local

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY src/CompanyBrain.MultiTenant/CompanyBrain.MultiTenant.csproj src/CompanyBrain.MultiTenant/

# Restore
RUN dotnet restore src/CompanyBrain.MultiTenant/CompanyBrain.MultiTenant.csproj

# Copy source
COPY src/CompanyBrain.MultiTenant/ src/CompanyBrain.MultiTenant/

# Build
WORKDIR /src/src/CompanyBrain.MultiTenant
RUN dotnet build CompanyBrain.MultiTenant.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish CompanyBrain.MultiTenant.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create data directory
RUN mkdir -p /app/data

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CompanyBrain.MultiTenant.dll"]
