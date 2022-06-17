FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Maroon.IntegrationTesting.DockerClient.SampleApplication/Maroon.IntegrationTesting.DockerClient.SampleApplication.csproj", "Maroon.IntegrationTesting.DockerClient.SampleApplication/"]
RUN dotnet restore "Maroon.IntegrationTesting.DockerClient.SampleApplication/Maroon.IntegrationTesting.DockerClient.SampleApplication.csproj"

COPY Maroon.IntegrationTesting.DockerClient.SampleApplication Maroon.IntegrationTesting.DockerClient.SampleApplication/
WORKDIR "/src/Maroon.IntegrationTesting.DockerClient.SampleApplication"
RUN dotnet build "Maroon.IntegrationTesting.DockerClient.SampleApplication.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Maroon.IntegrationTesting.DockerClient.SampleApplication.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Maroon.IntegrationTesting.DockerClient.SampleApplication.dll"]
