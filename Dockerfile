FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем решение и восстанавливаем зависимости
COPY blog.sln .
COPY blog/*.csproj ./blog/
RUN dotnet restore

# Копируем весь код
COPY . .
WORKDIR /src/blog
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Открываем порт, который слушает приложение
EXPOSE 80

ENTRYPOINT ["dotnet", "blog.dll"]
