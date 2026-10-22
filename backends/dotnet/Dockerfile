# Build from backends/dotnet:   docker build -t fsidentity-api .
# Run:  docker run -p 8080:8080 -e ConnectionStrings__Default="..." -e Jwt__Key="<at least 32 characters>" fsidentity-api
# The schema is not created on start: apply the migrations first (README, "Create the schema").
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json FSIdentity.slnx ./
COPY src/FSIdentity.Domain/*.csproj src/FSIdentity.Domain/
COPY src/FSIdentity.Identity/*.csproj src/FSIdentity.Identity/
COPY src/FSIdentity.Api/*.csproj src/FSIdentity.Api/
RUN dotnet restore src/FSIdentity.Api/FSIdentity.Api.csproj
COPY src/ src/
RUN dotnet publish src/FSIdentity.Api/FSIdentity.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FSIdentity.Api.dll"]
