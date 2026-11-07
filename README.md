[![Build](https://github.com/ausil/sImlac/actions/workflows/build.yml/badge.svg)](https://github.com/ausil/sImlac/actions/workflows/build.yml)

# sImlac

An Imlac PDS-1 / PDS-4 emulator.

## Building

sImlac targets .NET 10 and builds on Linux (x86_64 and aarch64) and Windows.

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) (or newer), then:

    dotnet build imlac.sln -c Release

To run from the source tree:

    dotnet run --project imlac

To produce a self-contained executable for a given platform:

    dotnet publish imlac/imlac.csproj -c Release -r linux-x64   --self-contained -p:PublishSingleFile=true -o publish/linux-x64
    dotnet publish imlac/imlac.csproj -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o publish/linux-arm64
    dotnet publish imlac/imlac.csproj -c Release -r win-x64     --self-contained -p:PublishSingleFile=true -o publish/win-x64

## SDL2

The display uses SDL2. A bundled SDL2 library is included for Windows x64 and
Linux x86_64. On Linux aarch64 (and as a fallback elsewhere) install SDL2 from
your distribution:

    sudo dnf install sdl2-compat    # Fedora
    sudo apt install libsdl2-2.0-0  # Debian / Ubuntu
