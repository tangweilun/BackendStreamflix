FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy the csproj and restore
COPY ["Streamflix.csproj", "./"]
RUN dotnet restore "Streamflix.csproj"

# ✅ Install dotnet-ef tool globally
RUN dotnet tool install --global dotnet-ef

# ✅ Add dotnet-ef to the PATH
ENV PATH="$PATH:/root/.dotnet/tools"

COPY . .
WORKDIR "/src"
RUN dotnet build "Streamflix.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Streamflix.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Streamflix.dll"]