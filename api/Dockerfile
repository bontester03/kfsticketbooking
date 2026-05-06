FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KfsBooking.sln ./
COPY Directory.Build.props ./
COPY src/KfsBooking.Domain/*.csproj src/KfsBooking.Domain/
COPY src/KfsBooking.Application/*.csproj src/KfsBooking.Application/
COPY src/KfsBooking.Infrastructure/*.csproj src/KfsBooking.Infrastructure/
COPY src/KfsBooking.Api/*.csproj src/KfsBooking.Api/
COPY tests/KfsBooking.Tests/*.csproj tests/KfsBooking.Tests/

RUN dotnet restore src/KfsBooking.Api/KfsBooking.Api.csproj

COPY . .

RUN dotnet publish src/KfsBooking.Api/KfsBooking.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# Default for local docker-compose; Railway / Heroku override via PORT and Program.cs honors it.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

RUN useradd --uid 1000 --create-home --shell /bin/bash kfs && \
    mkdir -p /app/logs && chown -R kfs:kfs /app
USER kfs

COPY --from=build --chown=kfs:kfs /app/publish .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "KfsBooking.Api.dll"]
