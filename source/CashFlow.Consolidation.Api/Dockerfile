FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
# hadolint ignore=DL3008
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["source/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj", "source/CashFlow.Consolidation.Api/"]
COPY ["source/CashFlow.Consolidation.Domain/CashFlow.Consolidation.Domain.csproj", "source/CashFlow.Consolidation.Domain/"]
COPY ["source/CashFlow.Consolidation.Infrastructure/CashFlow.Consolidation.Infrastructure.csproj", "source/CashFlow.Consolidation.Infrastructure/"]
COPY ["source/CashFlow.Shared.Contracts/CashFlow.Shared.Contracts.csproj", "source/CashFlow.Shared.Contracts/"]
RUN dotnet restore "source/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj"
COPY . .
RUN dotnet build "source/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "source/CashFlow.Consolidation.Api/CashFlow.Consolidation.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.Consolidation.Api.dll"]
