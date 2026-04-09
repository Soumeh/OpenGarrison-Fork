

## Layout

- Client plugin abstractions live under `Plugins/Client/OpenGarrison.Client.Plugins.Abstractions/`.
- Client plugin implementations live under `Plugins/Client/OpenGarrison.Client.Plugins.<PluginName>/`.
- Server plugin abstractions live under `Plugins/Server/OpenGarrison.Server.Plugins.Abstractions/`.
- Server plugin implementations live under `Plugins/Server/OpenGarrison.Server.Plugins.<PluginName>/`.

## Naming

- Project and assembly names should follow `OpenGarrison.Client.Plugins.<PluginName>` or `OpenGarrison.Server.Plugins.<PluginName>`.
- Abstraction projects should end in `.Abstractions`.
- The runtime plugin folder name should match the suffix after `OpenGarrison.{Client|Server}.Plugins.` when possible.

## Build And Packaging

- Plugin implementation projects own their own output path into the runtime plugin folders under `Client/bin/.../Plugins/Client/<PluginName>/` or `Server/bin/.../Plugins/Server/<PluginName>/`.
- `scripts/package.ps1` copies manifest-driven packaged Lua plugins from `Plugins/Packaged/Client/...` and `Plugins/Packaged/Server/...` into the shipped runtime `Plugins/Client/...` and `Plugins/Server/...` folders.
- App debug builds now mirror the packaged plugin layout by copying `Plugins/Packaged/Client/...` into `Client/bin/.../Plugins/Client/...` and `Plugins/Packaged/Server/...` into `Server/bin/.../Plugins/Server/...`.


## Runtime Conventions

- Client plugins are loaded from `Plugins/Client`.
- Server plugins are loaded from `Plugins/Server`.
- Both hosts support manifest-driven packaged plugins via `plugin.json`.
- Lua plugins have first-pass hosts on both client and server, but the
  exposed surfaces are intentionally bounded and engine-shaped rather than raw
  engine object access.
- Client plugin config should live under `config/plugins/client/<pluginId>/`.
- Server plugin config should live under `config/plugins/server/<pluginId>/`.
- Engine-side seam and runtime safety rules are defined in [PLUGIN_HOST_CONTRACT.md](C:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Plugins/PLUGIN_HOST_CONTRACT.md).

## Seam Expansion Rubric

- Add a new Lua seam when the requested capability is reusable, engine-shaped, and likely to benefit more than one plugin.
- Add a new Lua seam when the capability can be expressed as a stable callback, service, DTO, asset registration surface, or bounded command.
- Do not add a seam that simply hands Lua broad access to unstable engine internals, raw object graphs, or authority it cannot safely own.
- Prefer service-style APIs over exposing engine objects directly.
- Prefer validated and bounded operations over arbitrary mutation.
- If a requested feature would make the host API materially messier for everyone else while only serving one niche plugin, stop and reassess the design before exposing it.

## Lua Plugin Scope

- Client Lua is currently suited for presentation plugins such as menu helpers,
  background overrides, HUD widgets, scoreboard panels, simple audio cues, and
  config-backed options.
- Server Lua is currently suited for passive/event-driven plugins plus bounded
  mutations through admin operations, replicated state, and client messaging.
- Lua authoring templates live under `Plugins/Templates/`.
- Ready-to-run packaged Lua examples live under `Plugins/Packaged/` and are copied into packaged runtime outputs.

## Profiling

- Set `OG2_CLIENT_PLUGIN_PROFILE=1` before launching the client to emit periodic aggregate plugin hook timings to the console/log.
- The current client host logs the hottest hooks every 5 seconds in the form `[plugin-profile] plugin=<id> hook=<stage> type=<hookType> calls=<count> totalMs=<total> avgMs=<avg> maxMs=<max>`.
- Use packaged/dist-style runtime layouts when profiling so Lua and CLR are not mixed accidentally.

