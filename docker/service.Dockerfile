# One Dockerfile for every BuildConnect .NET service (Linux).
# Build context: repository root. Example:
#   docker build -f docker/service.Dockerfile --build-arg SERVICE=UserService -t buildconnect/userservice .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG SERVICE
WORKDIR /src

# Restore first, from project files only, so the NuGet layer is cached until a csproj changes.
COPY BuildingBlocks/BuildingBlocks.csproj BuildingBlocks/
COPY ${SERVICE}/${SERVICE}.csproj ${SERVICE}/
RUN dotnet restore ${SERVICE}/${SERVICE}.csproj

COPY BuildingBlocks/ BuildingBlocks/
COPY ${SERVICE}/ ${SERVICE}/
RUN dotnet publish ${SERVICE}/${SERVICE}.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
ARG SERVICE
# curl is only used by the container health check (GET /health/ready).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    SERVICE_DLL=${SERVICE}.dll
EXPOSE 8080
# Run as the non-root user that the .NET images provide.
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$SERVICE_DLL\""]
