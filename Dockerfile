FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Build app
WORKDIR /build
COPY src/ExtShiftingApp/ExtShiftingApp.csproj ./ExtShiftingApp/
RUN dotnet restore ExtShiftingApp/ExtShiftingApp.csproj
COPY src/ExtShiftingApp/ ./ExtShiftingApp/
RUN dotnet publish ExtShiftingApp/ExtShiftingApp.csproj -c Release -o /app --no-restore

FROM ubuntu:22.04

ARG DEBIAN_FRONTEND=noninteractive
ENV ASPNETCORE_URLS=http://+:5000

# Install Macaulay2
RUN apt-get update && \
    apt-get install -y software-properties-common && \
    add-apt-repository ppa:macaulay2/macaulay2 && \
    apt-get update && \
    apt-get install -y macaulay2 && \
    apt-get purge -y --auto-remove software-properties-common && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Install .NET 8
RUN apt-get update && \
    apt-get install -y wget && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y aspnetcore-runtime-8.0 && \
    apt-get purge -y --auto-remove wget && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

COPY --from=build /app /app

# Copy M2 code (submodule)
COPY m2/ /m2/

WORKDIR /app
EXPOSE 5000

ENTRYPOINT ["dotnet", "ExtShiftingApp.dll"]
