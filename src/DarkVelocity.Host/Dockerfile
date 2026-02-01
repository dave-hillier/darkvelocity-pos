FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY DarkVelocity.slnx .
COPY src/DarkVelocity.Host/DarkVelocity.Host.csproj src/DarkVelocity.Host/

# Restore dependencies
RUN dotnet restore src/DarkVelocity.Host/DarkVelocity.Host.csproj

# Copy everything else and build
COPY src/DarkVelocity.Host/ src/DarkVelocity.Host/
RUN dotnet publish src/DarkVelocity.Host/DarkVelocity.Host.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5200
EXPOSE 5200 8888

ENTRYPOINT ["dotnet", "DarkVelocity.Host.dll"]
