FROM node:24-bookworm-slim AS node
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
COPY --from=node /usr/local/ /usr/local/
WORKDIR /source
COPY . .
RUN dotnet restore src/WhatHaveIDone.API/WhatHaveIDone.API.csproj
RUN dotnet publish src/WhatHaveIDone.API/WhatHaveIDone.API.csproj -c Release --no-restore -o /publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13
WORKDIR /app
USER root
RUN mkdir -p /data/keys /data/imports && chown -R app:app /data
COPY --from=build /publish .
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 DataProtection__Path=/data/keys Import__Path=/data/imports
ENTRYPOINT ["dotnet", "WhatHaveIDone.API.dll"]
