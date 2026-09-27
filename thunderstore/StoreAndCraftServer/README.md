# StoreAndCraftServer

Server-side auto-store for a **PC dedicated server**. Console / Game Pass clients install nothing.

Ground items near chests are pulled in on the server. Chest contents sync to everyone, including PS5 and Xbox.

**This is not the full StoreAndCraft mod.** Dump, middle-click, rename, search, and craft/build from chests need a client mod and will not work on consoles.

## Install

1. PC dedicated server with BepInExPack Valheim 5.4.2350+.
2. Put the plugin only on the **server** (`BepInEx/plugins/StoreAndCraftServer/`).
3. Do **not** install `StoreAndCraft` on the same server.
4. Clients stay vanilla.

Crossplay (`-crossplay`): BepInEx must still load on the dedicated process. If the loader never starts, this mod cannot run. Test with a server log line `StoreAndCraftServer v1.0.0 loaded`.

## Config

`BepInEx/config/StoreAndCraftServer.cfg` and `StoreAndCraftServer.rules.yml` (store ranges / allow / deny).

Wards: a chest inside a ward only fills while a permitted player is online.

This package contains AI-generated code.
