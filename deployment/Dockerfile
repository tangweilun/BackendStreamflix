FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy and publish app and libraries
COPY . ./
# Run migrations during build
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "Streamflix.dll"]