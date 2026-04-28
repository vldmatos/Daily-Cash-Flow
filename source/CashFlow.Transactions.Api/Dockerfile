FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["source/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj", "source/CashFlow.Transactions.Api/"]
COPY ["source/CashFlow.Transactions.Domain/CashFlow.Transactions.Domain.csproj", "source/CashFlow.Transactions.Domain/"]
COPY ["source/CashFlow.Transactions.Infrastructure/CashFlow.Transactions.Infrastructure.csproj", "source/CashFlow.Transactions.Infrastructure/"]
COPY ["source/CashFlow.Shared.Contracts/CashFlow.Shared.Contracts.csproj", "source/CashFlow.Shared.Contracts/"]
RUN dotnet restore "source/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj"
COPY . .
RUN dotnet build "source/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "source/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.Transactions.Api.dll"]
