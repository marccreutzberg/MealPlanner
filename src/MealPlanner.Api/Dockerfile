# Build context is the repository root (see AppHost.cs), so shared props and ServiceDefaults are available.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY Directory.Build.props Directory.Packages.props ./
COPY src/MealPlanner.ServiceDefaults/MealPlanner.ServiceDefaults.csproj src/MealPlanner.ServiceDefaults/
COPY src/MealPlanner.Api/MealPlanner.Api.csproj src/MealPlanner.Api/
RUN dotnet restore src/MealPlanner.Api/MealPlanner.Api.csproj

COPY src/MealPlanner.ServiceDefaults/ src/MealPlanner.ServiceDefaults/
COPY src/MealPlanner.Api/ src/MealPlanner.Api/
RUN dotnet publish src/MealPlanner.Api/MealPlanner.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "MealPlanner.Api.dll"]
