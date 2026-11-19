# The API, on .NET 11. Built from the root of the repository, which holds the projects it references:
#
#   docker build -f PhotoMap.Api/PhotoMap.Api/Dockerfile -t photo-map-api .
#
# or with docker compose (the app profile), see docker-compose.yml.

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# the project files first: the restore is cached until one of them changes, not on every change of the code
COPY global.json Directory.Build.props ./
COPY PhotoMap.Shared/PhotoMap.Shared.csproj PhotoMap.Shared/
COPY PhotoMap.Worker/PhotoMap.Worker.csproj PhotoMap.Worker/
COPY PhotoMap.Api/PhotoMap.Api.Domain/PhotoMap.Api.Domain.csproj PhotoMap.Api/PhotoMap.Api.Domain/
COPY PhotoMap.Api/PhotoMap.Api.Database/PhotoMap.Api.Database.csproj PhotoMap.Api/PhotoMap.Api.Database/
COPY PhotoMap.Api/PhotoMap.Api.Services/PhotoMap.Api.Services.csproj PhotoMap.Api/PhotoMap.Api.Services/
COPY PhotoMap.Api/PhotoMap.Api/PhotoMap.Api.csproj PhotoMap.Api/PhotoMap.Api/
RUN dotnet restore PhotoMap.Api/PhotoMap.Api/PhotoMap.Api.csproj

COPY PhotoMap.Shared/ PhotoMap.Shared/
COPY PhotoMap.Worker/ PhotoMap.Worker/
COPY PhotoMap.Api/ PhotoMap.Api/
RUN dotnet publish PhotoMap.Api/PhotoMap.Api/PhotoMap.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app

# Serilog writes its file to /logs; the image runs as the non-root app user, who must own it
RUN mkdir /logs && chown "$APP_UID" /logs

COPY --from=build /app .

USER $APP_UID
# HTTP only, on 8080 (ASPNETCORE_HTTP_PORTS of the image); TLS is left to whatever is in front of it
EXPOSE 8080
ENTRYPOINT ["dotnet", "PhotoMap.Api.dll"]
