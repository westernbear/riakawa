# Development

Riakawa targets Terraria 1.4.4.9 and tModLoader 2026.08.3.0. Set up the
installed game at `.tools/tModLoader`, .NET at `.tools/dotnet`, and Pillow in
`.tools/assets-env`. Then run:

```sh
bash scripts/build.sh
```

The build checks the five character sheets and the appearance packet, compiles
the mod, and writes `dist/Riakawa.tmod`. Set `TML_PATH` or `ASSET_PYTHON` if
your tools live elsewhere.

Chiikawa, Usagi and Momonga use 40 × 56 pixel frames. Hachiware and Doro use
80 × 112 pixel frames drawn at half scale. The rows in
`Riakawa/Assets/manifest.json` map idle, walk, jump, swim, attack, mount, hurt
and death. Doro has one extra run row. `CharacterLayer` draws the selected
frame while vanilla keeps the held item, wings, mount, sounds, damage and
collision. `RiakawaPlayer` synchronizes the character choice in multiplayer.

Doro's four-legged run starts at the same speed threshold the installed game
uses for Hermes-style running dust and sound: horizontal speed above the
midpoint of `maxRunSpeed` and `accRunSpeed`, while moving on the ground.
The helper in `Core/CosmeticState.cs` is checked by `checks/Program.cs`.

Hachiware and Doro were made with the PixelLab API from the supplied character
references. PixelLab generated the base sprites and movement frames. The frames
were aligned to the sheet grid, and Doro's eyes and running mouth were retouched.
The supplied animation guided Doro's running pose. The API key stays outside
the repository.
