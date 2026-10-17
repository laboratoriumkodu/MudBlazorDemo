# Multi-stage build for MudBlazor app
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-debian-12 AS build
WORKDIR /src

# Copy solution and project files
COPY MudBlazorDemo.sln .
COPY MudBlazorDemo/MudBlazorDemo.csproj MudBlazorDemo/
COPY MudBlazorDemo.Client/MudBlazorDemo.Client.csproj MudBlazorDemo.Client/

# Restore dependencies
RUN dotnet restore MudBlazorDemo.sln

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish MudBlazorDemo/MudBlazorDemo.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-debian-12-slim

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -u 1001 appuser

WORKDIR /app
COPY --from=build /app/publish .

# Set ownership to app user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set ASP.NET Core URLs
ENV ASPNETCORE_URLS=http://+:8080

# Run the app
ENTRYPOINT ["dotnet", "MudBlazorDemo.dll"]
