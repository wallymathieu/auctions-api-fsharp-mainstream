FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# Copy solution and project files
COPY *.sln .
COPY AuctionSite.Domain/*.fsproj ./AuctionSite.Domain/
COPY AuctionSite.Persistence/*.fsproj ./AuctionSite.Persistence/
COPY AuctionSite.WebApi/*.fsproj ./AuctionSite.WebApi/
COPY AuctionSite.Tests/*.fsproj ./AuctionSite.Tests/

# Restore as distinct layers
RUN dotnet restore

# Copy everything else and build
COPY AuctionSite.Domain/. ./AuctionSite.Domain/
COPY AuctionSite.Persistence/. ./AuctionSite.Persistence/
COPY AuctionSite.WebApi/. ./AuctionSite.WebApi/
COPY AuctionSite.Tests/. ./AuctionSite.Tests/

# Build and test
RUN dotnet build -c Release --no-restore
RUN dotnet test -c Release --no-build

# Publish
RUN dotnet publish -c Release -o out --no-build AuctionSite.WebApi

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app/out .

# Create directory for events
RUN mkdir -p /app/tmp

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "AuctionSite.WebApi.dll"]
