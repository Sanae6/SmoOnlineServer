#!/bin/bash

TOKEN="SECRET_TOKEN_12345"
HOST="localhost"
PORT="1027"

DIR=`dirname "$0"`
[ -f "$DIR/test.env" ] && source "$DIR/test.env"

TYPE="${1:-Status}"

DATA=""
if [ $# -gt 1 ] ; then
  DATA=",\"Data\":\"${@:2}\""
fi

# backwards compatible way to dynamically trim binary before the first '{' character
function trim_start () {
  local IFS
  local LC_ALL
  local c
  while IFS= LC_ALL=C read -rd '' -n1 c ; do
    [ "$c" == "{" ] && echo -n "$c" && break
  done
  while IFS= LC_ALL=C read -rd '' -n1 c ; do
    echo -n "$c"
  done
}

echo -n "{\"API_JSON_REQUEST\":{\"Token\":\"${TOKEN}\",\"Type\":\"$TYPE\"$DATA}}"  \
  | timeout 5.0 nc $HOST $PORT  \
  | trim_start  \
  | jq  \
;
echo ""
