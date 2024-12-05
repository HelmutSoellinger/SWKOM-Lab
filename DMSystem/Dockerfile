# Base image for runtime dependencies
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy and restore the main project
COPY ["DMSystem/DMSystem.csproj", "DMSystem/"]
RUN dotnet restore "./DMSystem/DMSystem.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/DMSystem"

# Build the application
RUN dotnet build "./DMSystem.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DMSystem.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app

# Copy published files from publish stage
COPY --from=publish /app/publish .

# Copy appsettings.json to the shared volume
COPY DMSystem/appsettings.json /app/config/appsettings.json

# Set entry point
ENTRYPOINT ["dotnet", "DMSystem.dll"]
