FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HoshiBot.slnx ./
COPY src/HoshiBot.Host/HoshiBot.Host.csproj src/HoshiBot.Host/
COPY src/HoshiBot.Discord/HoshiBot.Discord.csproj src/HoshiBot.Discord/
COPY src/HoshiBot.Domain/HoshiBot.Domain.csproj src/HoshiBot.Domain/
COPY src/HoshiBot.Data/HoshiBot.Data.csproj src/HoshiBot.Data/
COPY src/HoshiBot.Scheduling/HoshiBot.Scheduling.csproj src/HoshiBot.Scheduling/
RUN dotnet restore src/HoshiBot.Host/HoshiBot.Host.csproj

COPY src/ src/
RUN dotnet publish src/HoshiBot.Host/HoshiBot.Host.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "HoshiBot.Host.dll"]
