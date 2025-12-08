FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app
COPY . .
RUN dotnet publish -c release --self-contained -r linux-x64 -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:PublishTrimmed=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final

# Install only required system libraries (GDI+ for image processing)
RUN apt-get update && apt-get install -y \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/bin/release/net8.0/linux-x64/publish /app
COPY --from=build /app/Resources /app/Resources/
COPY --from=build /app/static /static/
COPY --from=build /app/wwwroot /wwwroot/
WORKDIR /app
ENV PORT=80
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV Logging__LogLevel__Microsoft=Information

CMD [ "/app/Svema" ]