FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app
COPY . .
#RUN dotnet publish -c release --self-contained -r linux-musl-x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=true
RUN dotnet publish -c release --self-contained -r linux-musl-x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=false

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine AS final

RUN apk add --no-cache icu-libs

COPY --from=build /app/bin/release/net6.0/linux-musl-x64/publish/svema /usr/local/bin/svema
COPY --from=build /app/static /static/

ENV PORT=80
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV Logging__LogLevel__Microsoft=Information

CMD [ "/usr/local/bin/svema" ]