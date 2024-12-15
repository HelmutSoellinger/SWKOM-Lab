# Base image for runtime dependencies
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY DMSystem.ElasticSearch/*.csproj ./DMSystem.ElasticSearch/
COPY ../DMSystem.Messaging/*.csproj ./DMSystem.Messaging/
COPY ../DMSystem.Contracts/*.csproj ./DMSystem.Contracts/

# Restore dependencies
RUN dotnet restore ./DMSystem.ElasticSearch/DMSystem.ElasticSearch.csproj

# Copy the full source code
COPY DMSystem.ElasticSearch/ ./DMSystem.ElasticSearch/
COPY ../DMSystem.Messaging/ ./DMSystem.Messaging/
COPY ../DMSystem.Contracts/ ./DMSystem.Contracts/

# Build and publish the application
WORKDIR /src/DMSystem.ElasticSearch
RUN dotnet build "./DMSystem.ElasticSearch.csproj" -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish "./DMSystem.ElasticSearch.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish ./

# Set entry point for the application
ENTRYPOINT ["dotnet", "DMSystem.ElasticSearch.dll"]
