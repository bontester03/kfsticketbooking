FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KFS.sln ./
COPY Directory.Build.props ./
COPY src/KFS.Domain/*.csproj src/KFS.Domain/
COPY src/KFS.Application/*.csproj src/KFS.Application/
COPY src/KFS.Infrastructure/*.csproj src/KFS.Infrastructure/
COPY src/KFS.Api/*.csproj src/KFS.Api/
COPY tests/KFS.Tests/*.csproj tests/KFS.Tests/

RUN dotnet restore src/KFS.Api/KFS.Api.csproj

COPY . .
RUN dotnet publish src/KFS.Api/KFS.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# QuestPDF / SkiaSharp need libfontconfig at runtime on the Microsoft slim image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 1000 --create-home --shell /bin/bash kfs \
    && mkdir -p /app/logs /app/wwwroot && chown -R kfs:kfs /app

USER kfs

COPY --from=build --chown=kfs:kfs /app/publish .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "KFS.Api.dll"]
