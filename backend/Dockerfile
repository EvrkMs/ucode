# Frontend build
FROM node:20-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend .
RUN npm run build

# Backend build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Ucode.Backend.csproj backend/
RUN dotnet restore backend/Ucode.Backend.csproj
COPY backend ./backend
COPY --from=frontend-build /src/frontend/dist ./backend/wwwroot
RUN dotnet publish backend/Ucode.Backend.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Install Kerberos/GSSAPI libs required by Npgsql/Postgres
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
# Копируем сертификаты для Kestrel HTTPS (путь /app/certs)
COPY backend/certs ./certs
EXPOSE 5001
ENTRYPOINT ["dotnet", "Ucode.Backend.dll"]
