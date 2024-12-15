# Base stage: Runtime environment with dependencies
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Install required libraries
RUN apt-get update && apt-get install -y --no-install-recommends \
    tesseract-ocr \
    libc6-dev \
    libjpeg62-turbo-dev && \
    apt-get install -y --no-install-recommends \
    libleptonica-dev \
    libtesseract-dev \
    ghostscript && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Ensure libdl is properly linked
RUN ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so

# Add symbolic links for required libraries
WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 libleptonica-1.82.0.so && \
    ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 libtesseract50.so

# Build stage: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only project files first for caching
COPY *.sln ./
COPY DMSystem.OCRWorker/*.csproj ./DMSystem.OCRWorker/
COPY DMSystem.Contracts/*.csproj ./DMSystem.Contracts/
COPY DMSystem.Messaging/*.csproj ./DMSystem.Messaging/
# Restore dependencies
RUN dotnet restore ./DMSystem.OCRWorker/DMSystem.OCRWorker.csproj

# Copy the rest of the application source code
COPY . ./

# Build and publish the project
WORKDIR /src/DMSystem.OCRWorker
RUN dotnet publish -c Release -o /app

# Final stage: Production-ready image
FROM base AS final
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "DMSystem.OCRWorker.dll"]
