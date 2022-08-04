# Super Mario Odyssey: Online Server

The official server for the [Super Mario Odyssey: Online](https://github.com/CraftyBoss/SuperMarioOdysseyOnline) mod.


## Windows Setup

1. Download latest build from [Releases](https://github.com/Sanae6/SmoOnlineServer/releases)
2. Run `Server.exe`
3. `settings.json` is autogenerated in step 2, modify it however you'd like.

## Building (Mac/Linux Setup)

Must have the [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download) and Git installed.
Run these commands in your shell:
```shell
git clone https://github.com/Sanae6/SmoOnlineServer
cd SmoOnlineServer
# replace run with build to only build the server
dotnet run --project Server/Server.csproj -c Release
```
If you ran `dotnet build` instead of `dotnet run`, you can find the binary at `Server/bin/net6.0/Release/Server.exe`

## Running under systemd

If you have systemd, you can use the existing systemd serivce.
```shell
cp smo.serivce /etc/systemd/system/smo.service
# edit ExecStart to your path for the server executable and change WorkingDirectory to the server directory
chmod +x filepath to the server executable
systemctl enable --now smo.service
# for TTY access i would recommand conspy but there is also reptyr, chvt
```

## Run docker image

If you have [docker](https://docs.docker.com/) on your system, you can use the existing docker image.
That way you don't have to build this server yourself or manually handle executables.

```shell
docker  run  --rm  -it  -p 1027:1027  -v "/$PWD/data/://data/"  ghcr.io/sanae6/smo-online-server
# on Windows, depending on the shell you're using, $PWD might not work. Use an absolute path instead.
```

To always check for and use the latest server version you can add `--pull=always` to the options.

Alternatively there's a `docker-compose.yml` for [docker-compose](https://docs.docker.com/compose/) to simplify the command line options:
```shell
# update server
docker-compose pull

# start server
docker-compose up -d

# open the server cli
docker attach `docker-compose ps -q` --sig-proxy=false

# watch server logs
docker-compose logs --tail=20 --follow

# stop server
docker-compose stop
```

## Commands

Run `help` to get what commands are available in the server console.
Run the `loadsettings` command in the console to update the settings without restarting.
Server address and port will require a server restart, but everything else should update when you run `loadsettings`.

[//]: # (TODO: Document all commands, possibly rename them too.)

## Settings

### Server
Address: the ip address of the server, default: 0.0.0.0 # this shouldn't be changed  
Port: the port of the server, default 1027  
Maxplayers: the max amount of players that can join, default: 8  
Flip: flips the player upside down, defaults: enabled: true, pov: both  
Scenario: sync's scenario's for all players on the server, default: false  
Banlist: banned people are unable to join the server, default: false  

### Discord
Token: the token of the bot you want to load into, default: null  
Prefix: the bot prefix to be used, default: $  
LogChannel: logs the server console to that channel, default: null  