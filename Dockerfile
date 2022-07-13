FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /source

# Copy all .csproj files and use `dotnet restore` to install dependencies.
# Doing this first will allow the dependencies to stay cached if the csproj
# files didn't change, even if other code did.
COPY *.sln .
COPY **/*.csproj ./
RUN dotnet sln list \
    | tail -n +3 \
    | xargs -I {} sh -c \
        'target="{}"; dir="${target%/*}"; file="${target##*/}"; mkdir -p -- "$dir"; mv -- "$file" "$target"'
RUN dotnet restore

# Copy the code over and build the application.
COPY . .
RUN dotnet publish Server -c Release -o /app --no-restore

# Build runtime image.
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build-env /app .
ENTRYPOINT ["dotnet", "Server.dll"]
