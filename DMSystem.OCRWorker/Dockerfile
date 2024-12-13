# Base image for runtime dependencies
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Install required libraries
RUN apt-get update && apt-get install -y \
    libleptonica-dev \
    libtesseract-dev \
    libc6-dev \
    libjpeg62-turbo-dev \
    libgdiplus \
    libpng-dev \
    cmake \
    build-essential \
    ghostscript \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Ensure libdl is properly linked
RUN ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so

# Add symbolic links for required libraries
WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 libleptonica-1.82.0.so && \
    ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 libtesseract50.so

# Set library path for runtime
ENV LD_LIBRARY_PATH="/usr/lib/x86_64-linux-gnu:/app/runtimes/linux-x64/native:$LD_LIBRARY_PATH"

# Build stage for compiling the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY DMSystem.OCRWorker/*.csproj ./DMSystem.OCRWorker/
COPY ../DMSystem.Messaging/*.csproj ./DMSystem.Messaging/
COPY ../DMSystem.DAL/*.csproj ./DMSystem.DAL/
COPY ../DMSystem/*.csproj ./DMSystem/
COPY ../DMSystem.ElasticSearch/*.csproj ./DMSystem.ElasticSearch/
COPY ../DMSystem.Contracts/*.csproj ./DMSystem.Contracts/

# Restore dependencies
RUN dotnet restore ./DMSystem.OCRWorker/DMSystem.OCRWorker.csproj

# Copy the full source
COPY DMSystem.OCRWorker/ ./DMSystem.OCRWorker/
COPY ../DMSystem.Messaging/ ./DMSystem.Messaging/
COPY ../DMSystem.DAL/ ./DMSystem.DAL/
COPY ../DMSystem/ ./DMSystem/
COPY ../DMSystem.ElasticSearch/ ./DMSystem.ElasticSearch/
COPY ../DMSystem.Contracts/ ./DMSystem.Contracts/

# Build and publish the application
WORKDIR /src/DMSystem.OCRWorker
RUN dotnet publish -c Release -o /app

# Final runtime image
FROM base AS final
WORKDIR /app

# Copy the compiled application
COPY --from=build /app ./

# Debug: Check shared library linking
RUN ldd /usr/lib/x86_64-linux-gnu/libdl.so

# Set the entry point for the application
ENTRYPOINT ["dotnet", "DMSystem.OCRWorker.dll"]
