FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY WebApplication1/*.csproj ./WebApplication1/
RUN dotnet restore WebApplication1/WebApplication1.csproj

# Copy everything else and build
COPY . .
RUN dotnet publish WebApplication1/WebApplication1.csproj -c Release -o out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Configure for Render - USE PORT VARIABLE
ENV ASPNETCORE_URLS=http://0.0.0.0:$PORT
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "WebApplication1.dll"]