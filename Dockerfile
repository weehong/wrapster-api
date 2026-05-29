# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy solution + central package management files first for better layer caching
COPY Directory.Build.props Directory.Packages.props Wrapsfer.slnx ./

COPY src/Wrapsfer.Api/Wrapsfer.Api.csproj \
     src/Wrapsfer.Api/

COPY src/Wrapsfer.Application/Wrapsfer.Application.csproj \
     src/Wrapsfer.Application/

COPY src/Wrapsfer.Domain/Wrapsfer.Domain.csproj \
     src/Wrapsfer.Domain/

COPY src/Wrapsfer.Infrastructure/Wrapsfer.Infrastructure.csproj \
     src/Wrapsfer.Infrastructure/

COPY src/Wrapsfer.Mailing/Wrapsfer.Mailing.csproj \
     src/Wrapsfer.Mailing/

# Restore dependencies
RUN dotnet restore src/Wrapsfer.Api/Wrapsfer.Api.csproj

# Copy the remaining source files
COPY src/ src/

# Publish API
RUN dotnet publish src/Wrapsfer.Api/Wrapsfer.Api.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app/publish

# Install EF tool
RUN dotnet tool install --global dotnet-ef --version 10.*

ENV PATH="${PATH}:/root/.dotnet/tools"

# Generate Linux EF migrations bundle
RUN dotnet ef migrations bundle \
    --project src/Wrapsfer.Infrastructure \
    --startup-project src/Wrapsfer.Api \
    --configuration ${BUILD_CONFIGURATION} \
    --self-contained \
    --target-runtime linux-x64 \
    --output /app/efbundle \
    --force

# =========================================================

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# CJK font for PDF report rendering. QuestPDF/SkiaSharp resolves "Noto Sans CJK SC" through
# fontconfig at render time; without it, Chinese characters in waybill PDFs become tofu.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-noto-cjk \
    && rm -rf /var/lib/apt/lists/*

# Copy published API
COPY --from=build \
    --chown=app:app \
    /app/publish \
    ./

# Copy EF bundle
COPY --from=build \
    --chown=app:app \
    --chmod=0755 \
    /app/efbundle \
    ./efbundle

# Switch to non-root user AFTER copies
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "Wrapsfer.Api.dll"]