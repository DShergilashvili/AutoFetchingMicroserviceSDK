FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PresidentsMap.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# The database lives in /data: mount a volume there so it survives new releases.
ENV ConnectionStrings__Default="Data Source=/data/presidents.db" ASPNETCORE_URLS=http://+:8080
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "PresidentsMap.dll"]
