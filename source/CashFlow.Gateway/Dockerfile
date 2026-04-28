FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["source/CashFlow.Gateway/CashFlow.Gateway.csproj", "source/CashFlow.Gateway/"]
RUN dotnet restore "source/CashFlow.Gateway/CashFlow.Gateway.csproj"
COPY . .
RUN dotnet build "source/CashFlow.Gateway/CashFlow.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "source/CashFlow.Gateway/CashFlow.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.Gateway.dll"]
