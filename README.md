# FezStitcher

![banner](docs/banner.png)

FezStitcher is a [HAT](https://github.com/FEZModding/HAT) mod for [FEZ](https://www.fezgame.com/) which fixes some bugs in the vanilla game.

## Bug Fixes

* Fixes occasional game crash when re-entering Clock Tower
* Fixes softlock if Dot text appears while the camera is spiraling down
* Fixes game crash when ending the 32 cube end cutscene early while the pixelization effect is active (e.g. by change save slots or resetting a speedrun)
* Fixes the Sound Volume setting not being respected while in the 32 and 64 cube end cutscenes

## Usage

1. Download and install the [HAT](https://github.com/FEZModding/HAT) mod loader
2. Download FezStitcher from the Releases page and put the zip file into the `Mods` directory
3. Run FEZ with HAT and enjoy!

## Build Instructions

These are instructions on how to compile the mod yourself. If you just want to use the mod, see the Usage instructions above.

1. Clone this git repository
2. Copy `UserProperties.xml.template` into `UserProperties.xml` and edit it:
    * In the `FezDir` property, put a path to your FEZ game directory with HAT installed
3. With the .NET CLI, run `dotnet build` to build the mod and copy it into your FEZ `Mods` folder
    * You can run `dotnet build --configuration Release` to build in Release mode instead of Debug mode. Or, run `./scripts/generate_release.sh` to create a release zip file
    * Alternatively, instead of using the .NET CLI it should also be possible to open `FezStitcher.csproj` with Visual Studio on Windows and build it that way
