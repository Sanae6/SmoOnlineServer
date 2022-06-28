################################################################################
##################################################################   build   ###

FROM  mcr.microsoft.com/dotnet/sdk:6.0  as  build

WORKDIR  /app/

COPY  ./Server/  ./Server/
COPY  ./Shared/  ./Shared/

RUN  dotnet  publish  ./Server/Server.csproj  -c Release  -o ./out/

##################################################################   build   ###
################################################################################
################################################################   runtime   ###

FROM  mcr.microsoft.com/dotnet/runtime:6.0  as  runtime

WORKDIR  /app/

RUN   mkdir   /data/               \
  &&  touch   /data/settings.json  \
  &&  ln  -s  /data/settings.json  \
;

COPY  --from=build  /app/out/  ./

ENTRYPOINT  [ "dotnet", "Server.dll" ]

EXPOSE  1027/tcp
VOLUME  /data/

################################################################   runtime   ###
################################################################################
