FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["source/CashFlow.Consolidation.Service/CashFlow.Consolidation.Service.csproj", "source/CashFlow.Consolidation.Service/"]
COPY ["source/CashFlow.Consolidation.Domain/CashFlow.Consolidation.Domain.csproj", "source/CashFlow.Consolidation.Domain/"]
COPY ["source/CashFlow.Consolidation.Infrastructure/CashFlow.Consolidation.Infrastructure.csproj", "source/CashFlow.Consolidation.Infrastructure/"]
COPY ["source/CashFlow.Shared.Contracts/CashFlow.Shared.Contracts.csproj", "source/CashFlow.Shared.Contracts/"]
RUN dotnet restore "source/CashFlow.Consolidation.Service/CashFlow.Consolidation.Service.csproj"
COPY . .
RUN dotnet build "source/CashFlow.Consolidation.Service/CashFlow.Consolidation.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "source/CashFlow.Consolidation.Service/CashFlow.Consolidation.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.Consolidation.Service.dll"]
