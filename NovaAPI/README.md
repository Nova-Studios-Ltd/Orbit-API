# Orbit API

## Description
Orbit, a self-hosted communication platform designed with anonymity and privacy in mind. This repository contains the official API.

The API is fully cross-platform and built with C# and .NET 6

## Requirements
- [.NET 6](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- [docker (On the left pick your OS)](https://docs.docker.com/desktop/install/mac-install)
- [docker-compose](https://docs.docker.com/compose/install/)

## Getting the sources
Download a zipped version [here](https://github.com/Nova-Studios-Ltd/Orbit-Server/archive/refs/heads/master.zip) or clone via HTTPS:
```
git clone https://github.com/Nova-Studios-Ltd/Orbit-Server.git
```

## Running/Building
Note: If you wish to use your own instance of the client change API_Domain and Interface_Domain in appsettings.json in the src of the project

On linux you wont be able to run the API without sudo. Not sure why this is, but I haven't found a way to do it without needing sudo

docker and docker-compose will also require sudo (Unless you have set it up otherwise)

Before building with docker-compose make sure to configure the API. In `docker-compose.yml` provide the `MYSQL_ROOT_PASSWORD` and set the device fields of the volumes to a valid path.
In `appsettings.json` set the `Password` field under `SQLServerConfig` to the password you set as your `MYSQL_ROOT_PASSWORD`

If your aren't using docker-compose make sure to configure the `MYSQLServerConfig` to point to a working mysql server.

Running with dotnet:
```bash
 cd orbit-api
 dotnet run ./NovaAPI.sln --project ./NovaAPI/NovaAPI.csproj
 # Or with sudo
 sudo dotnet run ./NovaAPI.sln --project ./NovaAPI/NovaAPI.csproj
```

Building with dotnet
```bash
 cd orbit-api
 dotnet build ./NovaAPI.sln
 cd ./NovaAPI/bin/Debug/net6.0
 dotnet ./NovaAPI.dll
````

docker-compose:
```bash
 cd orbit-api/NovaAPI
 sudo docker-compose build
 
 sudo docker-compose up
 # or to run as a daemon
 sudo docker-compose up -d
```
The API itself is designed to run behind a running web server (such as apache) and doesn't run with SSL or https as the forward web server provides this. The official API is configured this way

## Roadmap
TODO: Add trello board

## Contributing
We are open to people contributing, we will have better defined guidelines later on!

## License
The project is currently licensed under a GPLv3 license

## Project status
Development is on going! Please be aware that both me (Nova1545) and my colleague (GentlyTech) currently work or are in school full time, so updates and changes maybe be slow at times
