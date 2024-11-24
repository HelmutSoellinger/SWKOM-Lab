FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for dependency resolution
COPY ["DMSystem.OCRWorker/DMSystem.OCRWorker.csproj", "DMSystem.OCRWorker/"]
COPY ["DMSystem/DMSystem.csproj", "DMSystem/"]
COPY ["DMSystem.OCR/DMSystem.OCR.csproj", "DMSystem.OCR/"]
COPY ["DMSystem.DAL/DMSystem.DAL.csproj", "DMSystem.DAL/"]
COPY ["DMSystem.Messaging/DMSystem.Messaging.csproj", "DMSystem.Messaging/"]

# Restore dependencies
RUN dotnet restore "DMSystem.OCRWorker/DMSystem.OCRWorker.csproj"

# Copy all project files
COPY DMSystem.OCRWorker/ DMSystem.OCRWorker/
COPY DMSystem/ DMSystem/
COPY DMSystem.OCR/ DMSystem.OCR/
COPY DMSystem.DAL/ DMSystem.DAL/
COPY DMSystem.Messaging/ DMSystem.Messaging/

# Copy the shared appsettings.json from DMSystem project
# Update the path as per the actual location of appsettings.json in the DMSystem project
COPY DMSystem/appsettings.json DMSystem.OCRWorker/appsettings.json

# Build the worker project
WORKDIR /src/DMSystem.OCRWorker
RUN dotnet build -c Release -o /app/build

# Publish the application
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish .

# Set entrypoint
ENTRYPOINT ["dotnet", "DMSystem.OCRWorker.dll"]
