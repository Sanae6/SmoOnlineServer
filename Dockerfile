################################################################################
##################################################################   build   ###

FROM  --platform=linux/amd64  mcr.microsoft.com/dotnet/sdk:6.0  as  build

WORKDIR  /app/

COPY  ./Server/  ./Server/
COPY  ./Shared/  ./Shared/

ARG TARGETARCH

# Download NuGet dependencies
RUN  dotnet  restore  \
    ./Server/Server.csproj  \
    -r debian.11-`echo $TARGETARCH | sed 's@^amd@x@'`  \
;

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

FROM  debian:11-slim  as  runtime

# Download & install additional runtime dependencies
RUN   apt-get  update  \
  &&  apt-get  install  -y  libicu67  \
  &&  rm  -rf  /var/lib/apt/lists/*  \
;

# Copy application binary from build stage
COPY  --from=build  /app/out/  /app/

ENTRYPOINT  [ "/app/Server" ]
EXPOSE      1027/tcp
WORKDIR     /data/
VOLUME      /data/

################################################################   runtime   ###
################################################################################
