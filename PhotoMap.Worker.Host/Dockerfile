# PhotoMap.Worker in a process of its own, on .NET 11. Built from the root of the repository, which holds the
# projects it references:
#
#   docker build -f PhotoMap.Worker.Host/Dockerfile -t photo-map-worker .
#
# or with docker compose (the app profile), see docker-compose.yml.

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# the project files first: the restore is cached until one of them changes, not on every change of the code
COPY global.json Directory.Build.props ./
COPY PhotoMap.Shared/PhotoMap.Shared.csproj PhotoMap.Shared/
COPY PhotoMap.Worker/PhotoMap.Worker.csproj PhotoMap.Worker/
COPY PhotoMap.Worker.Host/PhotoMap.Worker.Host.csproj PhotoMap.Worker.Host/
RUN dotnet restore PhotoMap.Worker.Host/PhotoMap.Worker.Host.csproj

COPY PhotoMap.Shared/ PhotoMap.Shared/
COPY PhotoMap.Worker/ PhotoMap.Worker/
COPY PhotoMap.Worker.Host/ PhotoMap.Worker.Host/
RUN dotnet publish PhotoMap.Worker.Host/PhotoMap.Worker.Host.csproj -c Release --no-restore -o /app

# the ASP.NET Core runtime, not the plain one: PhotoMap.Worker references the Microsoft.AspNetCore.App framework
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
ENTRYPOINT ["dotnet", "PhotoMap.Worker.Host.dll"]
