FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Contracts/Contracts.csproj Contracts/
COPY Bookings.Domain/Bookings.Domain.csproj Bookings.Domain/
COPY Bookings.Application/Bookings.Application.csproj Bookings.Application/
COPY Bookings.Infrastructure/Bookings.Infrastructure.csproj Bookings.Infrastructure/
COPY Bookings.Api/Bookings.Api.csproj Bookings.Api/
RUN dotnet restore Bookings.Api/Bookings.Api.csproj

COPY Contracts/ Contracts/
COPY Bookings.Domain/ Bookings.Domain/
COPY Bookings.Application/ Bookings.Application/
COPY Bookings.Infrastructure/ Bookings.Infrastructure/
COPY Bookings.Api/ Bookings.Api/
RUN dotnet publish Bookings.Api/Bookings.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Bookings.Api.dll"]
