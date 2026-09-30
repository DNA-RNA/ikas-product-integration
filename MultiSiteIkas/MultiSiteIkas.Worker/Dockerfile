FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ["MultiSiteIkas.Worker/MultiSiteIkas.Worker.csproj", "MultiSiteIkas.Worker/"]
COPY ["MultiSiteIkas.Core/MultiSiteIkas.Core.csproj", "MultiSiteIkas.Core/"]
COPY ["MultiSiteIkas.Data/MultiSiteIkas.Data.csproj", "MultiSiteIkas.Data/"]
COPY ["MultiSiteIkas.Jobs/MultiSiteIkas.Jobs.csproj", "MultiSiteIkas.Jobs/"]
RUN dotnet restore "MultiSiteIkas.Worker/MultiSiteIkas.Worker.csproj"

COPY . .
RUN dotnet publish "MultiSiteIkas.Worker/MultiSiteIkas.Worker.csproj" \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MultiSiteIkas.Worker.dll"]
