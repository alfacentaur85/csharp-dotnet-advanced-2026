FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Contracts/Contracts.csproj Contracts/
COPY Events.Domain/Events.Domain.csproj Events.Domain/
COPY Events.Application/Events.Application.csproj Events.Application/
COPY Events.Infrastructure/Events.Infrastructure.csproj Events.Infrastructure/
COPY Events.Api/Events.Api.csproj Events.Api/
RUN dotnet restore Events.Api/Events.Api.csproj

COPY Contracts/ Contracts/
COPY Events.Domain/ Events.Domain/
COPY Events.Application/ Events.Application/
COPY Events.Infrastructure/ Events.Infrastructure/
COPY Events.Api/ Events.Api/
RUN dotnet publish Events.Api/Events.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Events.Api.dll"]
