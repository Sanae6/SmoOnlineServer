#!/bin/bash
set -euo pipefail

if [[ "$#" == "0" ]] || [[ "$#" > "1" ]] || ! [[ "$1" =~ ^(all|x64|arm|arm64|win64)$ ]] ; then
  echo "Usage: docker-build.sh {all|x64|arm|arm64|win64}"
  exit 1
fi

DIR=$(dirname "$(realpath $0)")
cd "$DIR"

declare -A archs=(
  ["x64"]="linux-x64"
  ["arm"]="linux-arm"
  ["arm64"]="linux-arm64"
  ["win64"]="win-x64"
)

for sub in "${!archs[@]}" ; do
  arch="${archs[$sub]}"

  if [[ "$1" != "all" ]] && [[ "$1" != "$sub" ]] ; then
    continue
  fi

  docker  run                         \
    -u `id -u`:`id -g`                \
    -v "/$DIR/"://app/                \
    -w //app/                         \
    -e DOTNET_CLI_HOME=//app/cache/   \
    -e XDG_DATA_HOME=//app/cache/     \
    mcr.microsoft.com/dotnet/sdk:6.0  \
      dotnet  publish                 \
        ./Server/Server.csproj        \
        -r $arch                      \
        -c Release                    \
        -o /app/bin/$sub/             \
        --self-contained              \
        -p:publishSingleFile=true     \
  ;

  filename="Server"
  ext=""
  if   [[ "$sub" == "arm"   ]] ; then filename="Server.arm";
  elif [[ "$sub" == "arm64" ]] ; then filename="Server.arm64";
  elif [[ "$sub" == "win64" ]] ; then filename="Server.exe"; ext=".exe";
  fi

  mv  ./bin/$sub/Server$ext  ./bin/$filename
  rm  -rf  ./bin/$sub/
done
