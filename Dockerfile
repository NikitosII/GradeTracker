FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/EduTrack.Domain/EduTrack.Domain.csproj                                     src/EduTrack.Domain/
COPY src/EduTrack.Application/EduTrack.Application.csproj                           src/EduTrack.Application/
COPY src/EduTrack.Infrastructure.Persistence/EduTrack.Infrastructure.Persistence.csproj   src/EduTrack.Infrastructure.Persistence/
COPY src/EduTrack.Infrastructure.Messaging/EduTrack.Infrastructure.Messaging.csproj        src/EduTrack.Infrastructure.Messaging/
COPY src/EduTrack.Infrastructure.Telegram/EduTrack.Infrastructure.Telegram.csproj          src/EduTrack.Infrastructure.Telegram/
COPY src/EduTrack.Infrastructure.Scheduling/EduTrack.Infrastructure.Scheduling.csproj      src/EduTrack.Infrastructure.Scheduling/
COPY src/EduTrack.Infrastructure.Observability/EduTrack.Infrastructure.Observability.csproj src/EduTrack.Infrastructure.Observability/
COPY src/EduTrack.Bot.Web/EduTrack.Bot.Web.csproj                                   src/EduTrack.Bot.Web/
COPY src/EduTrack.Worker/EduTrack.Worker.csproj                                     src/EduTrack.Worker/
RUN dotnet restore src/EduTrack.Bot.Web/EduTrack.Bot.Web.csproj

COPY src/ src/
RUN dotnet publish src/EduTrack.Bot.Web/EduTrack.Bot.Web.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app ./
ENTRYPOINT ["dotnet", "EduTrack.Bot.Web.dll"]
