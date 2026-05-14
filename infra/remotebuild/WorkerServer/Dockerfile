FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

COPY worker-agent/Colosseum.WorkerAgent.csproj ./worker-agent/
RUN dotnet restore ./worker-agent/Colosseum.WorkerAgent.csproj

COPY worker-agent/ ./worker-agent/
RUN dotnet publish ./worker-agent/Colosseum.WorkerAgent.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble
WORKDIR /app

# Unity Linux Dedicated Server runtime dependencies.
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    libc6 \
    libgcc-s1 \
    libstdc++6 \
    zlib1g \
    libicu74 \
    libfontconfig1 \
    libglib2.0-0 \
    libnss3 \
    libxrandr2 \
    libasound2t64 \
    libxinerama1 \
    libxcursor1 \
    libxi6 \
    libxss1 \
    libxtst6 \
    libgtk-3-0 \
    libgbm1 \
    libdrm2 \
    libxdamage1 \
    libxfixes3 \
    libxcomposite1 \
    libxkbcommon0 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "Colosseum.WorkerAgent.dll"]
