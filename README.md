# Salty Chat for [RedM](https://redm.gg/)

[![Build Status](https://api.travis-ci.com/saltminede/saltychat-redm.svg?branch=master)](https://travis-ci.org/saltminede/saltychat-redm)

An example implementation of Salty Chat for [RedM](https://redm.gg/).

You can report bugs or make sugguestions via issues, or contribute via pull requests - we appreciate any contribution.

Join our [Discord](https://discord.gg/MBCnqSf) and start with [Salty Chat](https://www.saltmine.de/)!

# Setup Steps
1. Copy the folder `saltychat` into your resources
2. [Build the solution](https://github.com/saltminede/saltychat-docs/blob/master/installing-vs.md#installing-visual-studio) (`saltychat\SaltyChat-RedM.sln`) with Visual Studio 2019, so the `*.net.dll` files get build
3. Add `start saltychat` into your `server.cfg`
4. Open `fxmanifest.lua` and adjust the [variables](https://github.com/saltminede/saltychat-docs/blob/master/setup.md#config-variables)
```
VoiceEnabled "true"
ServerUniqueIdentifier "NMjxHW5psWaLNmFh0+kjnQik7Qc="
RequiredUpdateBranch ""
MinimumPluginVersion ""
SoundPack "default"
IngameChannelId "25"
IngameChannelPassword "5V88FWWME615"
```

# Keybinds
Description | Control | Default QWERTY
:---: | :---: | :---:
Toggle voice range | OpenJournal | J
Talk on primary radio | PushToTalk | N
Talk on secondary radio | OpenSatchelMenu | B

# Events
## Client
### SaltyChat_TalkStateChanged
Parameter | Type | Description
------------ | ------------- | -------------
isTalking | `bool` | `true` if player starts talking, `false` when the player stops talking

### SaltyChat_MicStateChanged
Parameter | Type | Description
------------ | ------------- | -------------
isMicrophoneMuted | `bool` | `true` if player mutes mic, `false` when the player unmutes mic

### SaltyChat_MicStateChanged
Parameter | Type | Description
------------ | ------------- | -------------
isSoundMuted | `bool` | `true` if player mutes sound, `false` when the player unmutes sound

# Exports
## Server
### EstablishCall
Starts a call between two players.

Parameter | Type | Description
------------ | ------------- | -------------
callerNetId | `int` | Server ID of the caller
partnerNetId | `int` | Server ID of the call partner

### EndCall
Ends a call between two players.

Parameter | Type | Description
------------ | ------------- | -------------
callerNetId | `int` | Server ID of the caller
partnerNetId | `int` | Server ID of the call partner

### SetPlayerRadioSpeaker
Turns radio speaker of an player on/off.

Parameter | Type | Description
------------ | ------------- | -------------
netId | `int` | Server ID of the player
toggle | `bool` | `true` to turn on speaker, `false` to turn it off

### SetPlayerRadioChannel
Sets a player's radio channel.

Parameter | Type | Description
------------ | ------------- | -------------
netId | `int` | Server ID of the player
radioChannelName | `string` | Name of the radio channel
isPrimary | `bool` | `true` to set the channel as primary, `false` to set it as secondary

### RemovePlayerRadioChannel
Removes a player from the radio channel.

Parameter | Type | Description
------------ | ------------- | -------------
netId | `int` | Server ID of the player
radioChannelName | `string` | Name of the radio channel

### SetRadioTowers
Sets the radio towers.

Parameter | Type | Description
------------ | ------------- | -------------
towers | `float[][]` | Array with radio tower positions (X, Y, Z)
