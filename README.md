# Salty Chat for [RedM](https://redm.gg/)

[![Build Status](https://api.travis-ci.com/saltminede/saltychat-redm.svg?branch=master)](https://travis-ci.org/saltminede/saltychat-redm)

An example implementation of Salty Chat for [RedM](https://redm.gg/).

You can report bugs or make sugguestions via issues, or contribute via pull requests - we appreciate any contribution.

Join our [Discord](https://discord.gg/MBCnqSf) and start with [Salty Chat](https://www.saltmine.de/)!

# Setup Steps
1. Copy the folder `saltychat` into your resources
2. Build the solution (`saltychat\SaltyChat-RedM.sln`) with Visual Studio 2019, so the `*.net.dll` files get build
3. Add `start saltychat` into your `server.cfg`
4. Open `fxmanifest.lua` and adjust the variables
```
VoiceEnabled "true"
ServerUniqueIdentifier "NMjxHW5psWaLNmFh0+kjnQik7Qc="
RequiredUpdateBranch ""
MinimumPluginVersion ""
SoundPack "default"
IngameChannelId "25"
IngameChannelPassword "5V88FWWME615"
```
