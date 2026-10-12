# ==========================================
# Stage 1: Build Vue 3 Frontend
# ==========================================
FROM node:22-alpine AS frontend-builder
WORKDIR /src/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# ==========================================
# Stage 2: Build .NET 10 Minimal API Backend
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-builder
WORKDIR /src/backend

COPY backend/Drop.Api/*.csproj ./Drop.Api/
RUN dotnet restore ./Drop.Api/Drop.Api.csproj

COPY backend/Drop.Api/ ./Drop.Api/

# Copy compiled frontend assets to wwwroot
COPY --from=frontend-builder /src/backend/Drop.Api/wwwroot ./Drop.Api/wwwroot

WORKDIR /src/backend/Drop.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Stage 3: Final Runtime Container
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Install ffmpeg for automatic clip thumbnail generation, duration probing, and web-friendly streaming
RUN apk add --no-cache ffmpeg

# Create persistent data directories
RUN mkdir -p /data/ingest /data/clips /data/thumbnails /data/db

COPY --from=backend-builder /app/publish .

# Environment Configuration
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DROP_ADMIN_USERNAME=admin \
    DROP_ADMIN_PASSWORD=change-me-promptly

VOLUME ["/data"]
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Drop.Api.dll"]
