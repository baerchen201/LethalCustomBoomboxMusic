## About

**This mod is intended to simplify the creation of mods, that add more songs to the Boombox.**

The original [Custom Boombox Music](https://thunderstore.io/c/lethal-company/p/Steven/Custom_Boombox_Music/) mod is not directly compatible with mods installed via Thunderstore Mod Manager or r2modman. This mod aims to fix that, and generally provide more stability and reliability.

This mod is completely compatible with most other mods for custom boombox music, however they will display as `Boombox <number> (Lethal Company)`.

Most of the time you can simply rename the folder from `Custom Songs` (or any other name) to `CustomBoomboxMusic` and it should be picked up by this mod without any other modifications.

## IMPORTANT (READ IF CREATING MODPACK)

**By default, this mod is completely client-sided, however it supports track synchronization.**

If everyone has this mod, simply disabling the `ClientSide` config value is recommended, as it will synchronize the currently playing track among all players.

That way it is guaranteed that everyone hears the same music, and there is basically no risk of the "playlist" de-syncing.

## Usage (creating your music mod)

Simply create a directory with the following files and folders (replace `MyPlugin` with your mod name):

```
MyPlugin
├── manifest.json (Mod information, see below)
├── README.md (Text displayed on mod page, like the one you're reading right now, can be empty)
├── icon.png (Your mod icon, must be 256x256 pixels)
└── BepInEx
    └── plugins
        └── CustomBoomboxMusic (Your music files go in here)
            ├── Artist - Title.mp3
            ├── Example Artist - Example Song (Example Game).ogg
            └── music.m4a
```

It is recommended to name your music files as shown (`Artist - Title (Source)`), since the user will see a "Now Playing" popup displaying the file name.

Example:
```
Now Playing:
Example Artist - Example Song (Example Game)
```

If synchronization is enabled, but you do not have the corresponding song loaded, a warning will be displayed and a placeholder song will play instead:
```
Missing audio:
Example Artist - Example Song (Example Game)
(CRC32: 12345678) could not be played
```

The following file extensions are supported:
 - `.mp3`
 - `.ogg` (vorbis)
 - `.wav`
 - `.m4a` (aac)
 - `.aiff`

### manifest.json:

The manifest.json file contains general information about your mod.

This is the general format:

```
{
  "name": Mod name,
  "version_number": Version number, in format major.minor.patch. This has to be increased every time you upload a new version of your mod,
  "website_url": Website url, can be left empty,
  "description": A short description of your mod,
  "dependencies": A list of dependencies, should be ["baer1-CustomBoomboxMusic-<VERSION>"]
}
```

Example:

```json
{
  "name": "MyPlugin",
  "version_number": "1.0.0",
  "website_url": "",
  "description": "My cool boombox music plugin",
  "dependencies": ["baer1-CustomBoomboxMusic-2.1.0"]
}
```

You can verify your manifest.json file [here](https://thunderstore.io/tools/manifest-v1-validator/).

### Uploading

Simply zip the contents of your folder and upload them to [thunderstore.io](https://thunderstore.io/package/create/)

You should be able to install it through your mod manager after about 3 hours

## Chat commands

This mod has optional support for chat commands, which allow you to play specific tracks at will or dynamically reload all audio files.

Command usages and examples:

```
/boo[mbox] r[eload] - completely reloads all audio files, freezes the game
/boo[mbox] l[ist] - list all loaded tracks
/boo[mbox] p[lay] <track> - Plays a specific track by CRC32 or name, Example: '/boo p Boombox 5' - plays boombox 5 from vanilla lethal company
/boo[mbox] v[ersion] - Displays version information
```

Simply installing [ChatCommandAPI](https://thunderstore.io/c/lethal-company/p/baer1/ChatCommandAPI/) will enable this feature
