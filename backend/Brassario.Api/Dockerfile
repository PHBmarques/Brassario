ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY Brassario.Api/Brassario.Api.csproj Brassario.Api/
RUN dotnet restore Brassario.Api/Brassario.Api.csproj

COPY Brassario.Api/ Brassario.Api/
RUN dotnet publish Brassario.Api/Brassario.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Brassario.Api.dll"]