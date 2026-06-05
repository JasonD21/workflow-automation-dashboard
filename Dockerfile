FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/WorkflowAutomation.Api/*.csproj src/WorkflowAutomation.Api/
RUN dotnet restore src/WorkflowAutomation.Api/WorkflowAutomation.Api.csproj
COPY . .
RUN dotnet publish src/WorkflowAutomation.Api/WorkflowAutomation.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WorkflowAutomation.Api.dll"]