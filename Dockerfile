# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Imagetextextraction.Backend.csproj", "./"]
RUN dotnet restore "./Imagetextextraction.Backend.csproj"
COPY . .
RUN dotnet build "Imagetextextraction.Backend.csproj" -c Release -o /app/build
RUN dotnet publish "Imagetextextraction.Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Serve the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 8080 (default for .NET 8 containers)
EXPOSE 8080

ENTRYPOINT ["dotnet", "Imagetextextraction.Backend.dll"]
