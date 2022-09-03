#!/bin/bash

TOKEN="SECRET_TOKEN_12345"
HOST="localhost"
PORT="1027"

DIR=`dirname "$0"`
[ -f "$DIR/test.env" ] && source "$DIR/test.env"

TYPE="$1"

DATA=""
if [ $# -gt 1 ] ; then
  DATA=",\"Data\":\"${@:2}\""
fi

echo -n "{\"API_JSON_REQUEST\":{\"Token\":\"${TOKEN}\",\"Type\":\"$TYPE\"$DATA}}"  \
  | timeout 5.0 nc $HOST $PORT  \
  | tail -c+23  \
;

echo ""
