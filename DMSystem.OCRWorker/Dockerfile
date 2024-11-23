# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files for dependency resolution
COPY ["DMSystem.OCRWorker/DMSystem.OCRWorker.csproj", "DMSystem.OCRWorker/"]
COPY ["DMSystem/DMSystem.csproj", "DMSystem/"]
COPY ["DMSystem.Messaging/DMSystem.Messaging.csproj", "DMSystem.Messaging/"]
COPY ["DMSystem.DAL/DMSystem.DAL.csproj", "DMSystem.DAL/"]

# Restore dependencies
RUN dotnet restore "DMSystem.OCRWorker/DMSystem.OCRWorker.csproj"

# Copy all project files
COPY DMSystem.OCRWorker/ DMSystem.OCRWorker/
COPY DMSystem/ DMSystem/
COPY DMSystem.Messaging/ DMSystem.Messaging/
COPY DMSystem.DAL/ DMSystem.DAL/

# Build the worker project
WORKDIR /src/DMSystem.OCRWorker
RUN dotnet build -c Release -o /app/build

# Publish the application
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DMSystem.OCRWorker.dll"]
