# https://hub.docker.com/_/microsoft-dotnet-sdk/
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine3.21 AS build

COPY . /app
WORKDIR /app
# Label as build image
LABEL "build"="labtracker"
# copy csproj and restore as distinct layers
RUN dotnet restore .

# copy everything else and build
RUN dotnet publish -c Release -o out

# https://hub.docker.com/_/microsoft-dotnet-runtime/
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine3.21 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "labtracker.dll"]
