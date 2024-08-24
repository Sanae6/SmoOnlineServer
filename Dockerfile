################################################################################
##################################################################   build   ###

FROM  --platform=linux/amd64  mcr.microsoft.com/dotnet/sdk:6.0  as  build

WORKDIR  /app/

COPY  ./Shared/Shared.csproj  ./Shared/Shared.csproj
COPY  ./Server/Server.csproj  ./Server/Server.csproj

ARG TARGETARCH

# Download NuGet dependencies
RUN  dotnet  restore  \
    ./Server/Server.csproj  \
    -r debian.11-`echo $TARGETARCH | sed 's@^amd@x@'`  \
;

COPY  ./Shared/  ./Shared/
COPY  ./Server/  ./Server/

# Build application binary
RUN  dotnet  publish  \
    ./Server/Server.csproj  \
    -r debian.11-`echo $TARGETARCH | sed 's@^amd@x@'`  \
    -c Release  \
    -o ./out/  \
    --no-restore  \
    --self-contained  \
    -p:publishSingleFile=true  \
;

##################################################################   build   ###
################################################################################
################################################################   runtime   ###

FROM  mcr.microsoft.com/dotnet/runtime:6.0  as  runtime

RUN adduser --disabled-password --home /home/container container

USER container
ENV  USER=container HOME=/home/container

# Copy application binary from build stage
COPY  --from=build  /app/out/  /app/

ENTRYPOINT  [ "/app/Server" ]
EXPOSE      1027/tcp
WORKDIR     /data/
VOLUME      /data/

################################################################   runtime   ###
################################################################################
