<p align="center">
  <img width="500" alt="osu! logo" src="assets/lazer.png">
</p>

# osu!tumthai

The community client for the **osu!somtum** private server — a fork of [osu!](https://github.com/ppy/osu) (*lazer*) built and tuned for playing on osu!somtum.

> 🚧 **Work in progress.** This client is under active development. Expect bugs, breaking changes, and features that are still being built. Please report issues and don't rely on it being stable yet.

It's the same osu! game you know, optimized for osu!somtum:

- **Connects to osu!somtum out of the box** — preconfigured to the osu!somtum server, no setup needed to start playing.
- **Relax / Autopilot (RX/AP) support** — browse RX/AP leaderboards on song select, view RX/AP profiles and rankings, and submit RX/AP scores.
- **Configurable server & avatar host** — advanced users can point the client at another compatible private server (e.g. a [g0v0-server](https://github.com/GooGuTeam/g0v0-server) instance) from in-game settings, no rebuild required.

---

## ⚠️ Important — please read before using

### Support osu! first

This is a **community fork** that builds on top of the work done by [ppy](https://github.com/ppy) and the osu! team. **Please support official osu! first:**

- Play and support the official client at **[osu.ppy.sh](https://osu.ppy.sh)**.
- Consider buying [osu!supporter](https://osu.ppy.sh/home/support) to back the people who make all of this possible.
- This fork would not exist without the original osu! project. All credit for the game itself goes to ppy and the osu! contributors.

### 🚫 Never connect this client to official osu!

> **Do NOT use this client to connect to `osu.ppy.sh` or `dev.ppy.sh`.**

This is a **third-party client** intended **only** for private servers. Connecting it to the official osu! servers (`osu.ppy.sh` / `dev.ppy.sh`) is against the [osu! rules](https://osu.ppy.sh/wiki/en/Rules) and **will get your official account restricted or banned.**

Use this client **only** with private servers you are permitted to play on. You are responsible for how you use it.

---

## Running

Grab a build for your platform from the [Releases](https://github.com/osu-somtum/osu-tumthai/releases) page.

On Linux, the release ships as an **AppImage** — make it executable and run it:

```shell
chmod +x osu-tumthai-x86_64.AppImage
./osu-tumthai-x86_64.AppImage
```

### Choosing a server

By default the client connects to **osu!somtum** — no configuration needed. To play on a different compatible private server, open **Settings → Online → Connection** and set:

- **Custom server URL** — the host of the private server (e.g. `osu.example.com`). Leave empty to use osu!somtum. A restart is required.
- **Custom avatar URL** — the host serving user avatars (avatars are looked up as `https://{host}/{userId}`). Leave empty to use the default.

---

## Building from source

### Prerequisites

- A desktop platform with the [.NET 8.0 SDK](https://dotnet.microsoft.com/download) installed.

An IDE with C# support is recommended: [JetBrains Rider](https://www.jetbrains.com/rider/), [Visual Studio](https://visualstudio.microsoft.com/vs/), or [Visual Studio Code](https://code.visualstudio.com/) with the C# Dev Kit.

### Get the source

```shell
git clone https://github.com/osu-somtum/osu-tumthai
cd osu-tumthai
```

### Run

```shell
dotnet run --project osu.Desktop
```

For performance testing or producing a build, add `-c Release`. If a build fails, try `dotnet restore` first. When loading in an IDE, prefer the `osu.Desktop.slnf` solution filter.

### Building a Linux AppImage

Publish a self-contained build, then package it with [`appimagetool`](https://github.com/AppImage/appimagetool):

```shell
# 1. publish (bundles the .NET runtime — no install needed to run)
dotnet publish osu.Desktop -c Release -r linux-x64 --self-contained -f net8.0 \
  -o "osu!.AppDir/usr/bin"

# 2. add AppRun, an osu!.desktop entry and an icon to osu!.AppDir/ , then:
ARCH=x86_64 appimagetool osu!.AppDir osu-tumthai-x86_64.AppImage
```

---

## Contributing

Issues and pull requests are welcome. For game-engine or gameplay changes that aren't specific to this fork, please consider contributing them upstream to [ppy/osu](https://github.com/ppy/osu) so the whole community benefits.

## Licence

This fork, like upstream osu!, is licensed under the [MIT licence](https://opensource.org/licenses/MIT). See [the licence file](LICENCE) for details. [tl;dr](https://tldrlegal.com/license/mit-license): do what you want, as long as you include the original copyright and licence notice.

This **does not** cover the "osu!" or "ppy" branding (software, resources, advertising, or promotion), which is protected by trademark law. Game resources are covered by a separate licence — see [ppy/osu-resources](https://github.com/ppy/osu-resources).

## Credits

- [osu!](https://github.com/ppy/osu) by [ppy](https://github.com/ppy) and contributors — the game this is built on. ❤️
- [torii-osu](https://github.com/ShikkesoraSIM/torii-osu) (the Torii client) — reference for the Relax / Autopilot private-server integration.
- [g0v0-server](https://github.com/GooGuTeam/g0v0-server) — the private-server software this client targets.
