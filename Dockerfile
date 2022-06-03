FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AntiDirtyWordBot/AntiDirtyWordBot.csproj", "AntiDirtyWordBot/"]
RUN dotnet restore "AntiDirtyWordBot/AntiDirtyWordBot.csproj"
COPY . .
WORKDIR "/src/AntiDirtyWordBot"
RUN dotnet build "AntiDirtyWordBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AntiDirtyWordBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AntiDirtyWordBot.dll"]