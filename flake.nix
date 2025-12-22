{
  description = "SMO Online Server Flake";
  inputs = {
    nixpkgs.url = "nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };
  outputs = {
    nixpkgs,
    flake-utils,
    ...
  }:
    flake-utils.lib.eachDefaultSystem (
      system: let
        pkgs = import nixpkgs {inherit system;};
        projectFile = "./Server/Server.csproj";
        testProjectFile = "./TestClient/TestClient.csproj";
        dotnet-sdk = pkgs.dotnet-sdk_6;
        dotnet-runtime = pkgs.dotnetCorePackages.runtime_6_0;
        version = "1.0.4";
        dotnetSixTool = dllOverride: toolName: let
          toolVersion = (builtins.fromJSON (builtins.readFile ./.config/dotnet-tools.json)).tools."${toolName}".version;
          sha256 = (builtins.head (builtins.filter (elem: elem.pname == toolName) ((import ./nix/deps.nix) {fetchNuGet = x: x;}))).sha256;
        in
          pkgs.stdenvNoCC.mkDerivation rec {
            name = toolName;
            version = toolVersion;
            nativeBuildInputs = [pkgs.makeWrapper];
            src = pkgs.fetchNuGet {
              inherit version sha256;
              pname = name;
              installPhase = ''mkdir -p $out/bin && cp -r tools/net6.0/any/* $out/bin'';
            };
            installPhase = let
              dll =
                if isNull dllOverride
                then name
                else dllOverride;
            in ''
              runHook preInstall
              mkdir -p "$out/lib"
              cp -r ./bin/* "$out/lib"
              makeWrapper "${dotnet-runtime}/bin/dotnet" "$out/bin/${name}" --add-flags "$out/lib/${dll}.dll"
              runHook postInstall
            '';
          };
      in {
        packages = {
          fantomas = dotnetSixTool null "fantomas";
          fsharp-analyzers = dotnetSixTool "FSharp.Analyzers.Cli" "fsharp-analyzers";
          default = pkgs.buildDotnetModule {
            inherit projectFile testProjectFile dotnet-sdk dotnet-runtime;
            pname = "SmoOnlineServer";
            version = version;
            src = ./.;
            nugetDeps = ./nix/deps.nix; # run `nix build .#default.passthru.fetch-deps && ./result` and put the result here
            doCheck = true;
          };
        };
        devShells = {
          default = pkgs.mkShell {
            buildInputs = [dotnet-sdk pkgs.git pkgs.alejandra pkgs.nodePackages.markdown-link-check];
          };
        };
      }
    );
}
