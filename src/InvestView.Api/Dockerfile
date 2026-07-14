FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY InvestView.sln ./
COPY src/InvestView.Domain/InvestView.Domain.csproj src/InvestView.Domain/
COPY src/InvestView.Application/InvestView.Application.csproj src/InvestView.Application/
COPY src/InvestView.Infrastructure/InvestView.Infrastructure.csproj src/InvestView.Infrastructure/
COPY src/InvestView.Api/InvestView.Api.csproj src/InvestView.Api/

RUN dotnet restore src/InvestView.Api/InvestView.Api.csproj

COPY . .
RUN dotnet publish src/InvestView.Api/InvestView.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InvestView.Api.dll"]
