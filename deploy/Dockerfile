# Build context is the repository root:
#   docker build -f deploy/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AnimeBackend.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# SQLite database and note attachments live here; mount it as a volume.
VOLUME /app/data

ENV ASPNETCORE_URLS=http://+:8080
ENV Urls=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AnimeBackend.Api.dll"]
