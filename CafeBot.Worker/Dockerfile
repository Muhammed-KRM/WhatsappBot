# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all source files
COPY CafeBot.Worker/ CafeBot.Worker/
COPY CafeBot.Business/ CafeBot.Business/
COPY CafeBot.Data/ CafeBot.Data/

# Restore and Publish
RUN dotnet publish CafeBot.Worker/CafeBot.Worker.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create data directory for SQLite
RUN mkdir -p /app/data

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CafeBot.Worker.dll"]
