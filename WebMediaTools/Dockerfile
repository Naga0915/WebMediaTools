#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
#install python3
RUN apt update && apt install -y python3

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["WebMediaTools/WebMediaTools.csproj", "WebMediaTools/"]
RUN dotnet restore "WebMediaTools/WebMediaTools.csproj"
COPY . .
WORKDIR "/src/WebMediaTools"
RUN dotnet build "WebMediaTools.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebMediaTools.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebMediaTools.dll"]ll jupyterlab