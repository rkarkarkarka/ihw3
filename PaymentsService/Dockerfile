FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PaymentsService/PaymentsService.csproj", "PaymentsService/"]
RUN dotnet restore "PaymentsService/PaymentsService.csproj"
COPY . .
WORKDIR "/src/PaymentsService"
RUN dotnet build "PaymentsService.csproj" -c Release -o /app/publish

FROM build AS publish
RUN dotnet publish "PaymentsService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Установка curl для health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentsService.dll"]
