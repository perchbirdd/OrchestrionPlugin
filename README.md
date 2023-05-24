[![Download count](https://img.shields.io/endpoint?url=https%3A%2F%2Fvz32sgcoal.execute-api.us-east-1.amazonaws.com%2Forchestrion)](https://github.com/lmcintyre/OrchestrionPlugin)
[![Build status](https://github.com/lmcintyre/OrchestrionPlugin/actions/workflows/build.yml/badge.svg)](https://github.com/lmcintyre/OrchestrionPlugin)
[![Latest release](https://img.shields.io/github/v/release/lmcintyre/OrchestrionPlugin)](https://github.com/lmcintyre/OrchestrionPlugin)

# OrchestrionPlugin (perchbird fork)
A plugin for [XIVLauncher](https://github.com/goaaats/FFXIVQuickLauncher) that adds a simple music player interface to control the in-game BGM,
allowing you to set it to any in-game track you want. The BGM will persist through **most** changes of zone/instance/etc, and usually will stay active until you change it or click Stop.
You can search for tracks by name or by assorted metadata, such as zone, instance or boss name where the track is played.

![Usage](https://github.com/ff-meli/OrchestrionPlugin/raw/master/gh/orch.gif)

_Note that this gif is very old, and is not representative of the current version of the plugin_

## FAQ
### Why are the song numbers skipping around?  They don't even start at 1!
Those numbers are the internal ids used by the game.  Many numbers do not correspond to playable tracks, and so I don't display them in the player.

### It's so hard to find certain tracks!  Can you add/change/remove (some specific info)?
All the song information in the player is auto-updated from [this spreadsheet](https://docs.google.com/spreadsheets/d/1qAkxPiXWF-EUHbIXdNcO-Ilo2AwLnqvdpW9tjKPitPY).
Feel free to comment in the document if you find any inconsistencies.

### Some new in-game music is out and I can't find it!
If the tracks are new, it is possible that either the spreadsheet has not been updated yet.

### I have a suggestion/issue/concern!
Mention it in the XL discord and @ perchbird, or create an issue on this repository.

## Credits
* ff-meli, for the original OrchestrionPlugin
* goat, for the launcher and dalamud, without which none of this would be possible.
* MagowDeath#1763 for maintaining [the previous spreadsheet](https://docs.google.com/spreadsheets/d/14yjTMHYmuB1m5-aJO8CkMferRT9sNzgasYq02oJENWs/edit#gid=0) with all of the song data that is used in this plugin.
* Many thanks to [Caraxi](https://github.com/Caraxi/) for keeping things working and updated while I (meli) was away!
* [Luna](https://github.com/LunaRyuko) for adding history and replacing columns with tables in the song list UI
* Too many discord people to name, for helping out with things and offering suggestions.
