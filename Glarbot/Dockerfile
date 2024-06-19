FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Glarbot/Glarbot.csproj", "Glarbot/"]
RUN dotnet restore "Glarbot/Glarbot.csproj"
COPY . .
WORKDIR "/src/Glarbot"
RUN dotnet build "Glarbot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Glarbot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Glarbot.dll"]