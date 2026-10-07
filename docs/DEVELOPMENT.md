# Development

The supported runtime is tModLoader **2026.08.3.0** with Terraria **1.4.4.9**.
The SDK version is pinned in `global.json`. Terraria/tModLoader binaries and native
reference images are not included in this repository.

Place the installed tModLoader files at `.tools/tModLoader`, the .NET SDK at
`.tools/dotnet`, and a Python environment with Pillow at `.tools/assets-env`.
`ffprobe` must be available for audio validation. Then run:

```sh
bash scripts/build.sh
```

`TML_PATH` and `ASSET_PYTHON` can point the build script to other local paths.
The Release build is written to `dist/Riakawa.tmod`. Steam Workshop rejects Debug builds. Rebuild the companion music pack without
regenerating music with:

```sh
python3 scripts/prepare-music.py --pack-only
```

For ordinary C# iteration, pass the installed tModLoader directory directly:

```sh
dotnet build Riakawa/Riakawa.csproj -c Release -p:TModLoaderDirectory=/path/to/tModLoader
```

`checks/GameChecks` and `checks/CombatProbe` are optional development mods, excluded
from Riakawa's package. They create isolated test observations; never enable their
world-editing commands in a real save. Recorded QA checks read local `artifacts/qa`
outputs. The published summaries describe the tested versions and limits in
[APPEARANCE-REVIEW.md](APPEARANCE-REVIEW.md).

## Momonga assets

`scripts/add-momonga.py` uses the existing weapon and attack catalog plus locally
exported Terraria reference images. It rebuilds Momonga's 36 animation frames and
426 weapon skins. Regeneration resets Momonga's voice links and review flags.
Restore the generated audio with `scripts/prepare-voices.py --characters Momonga`,
run `scripts/generate-weapon-sounds.cjs`, then review the rebuilt art before release.
The reference, generation settings and review records are in `assets/momonga-*.json`.

```sh
.tools/assets-env/bin/python checks/MomongaChecks.py
.tools/assets-env/bin/python checks/ArtChecks.py --poses artifacts/qa/momonga/poses
python3 checks/VoiceChecks.py --qa artifacts/qa/momonga --characters Momonga
```

## Combat replay

`CombatProbe` supports an opt-in single-player replay. Use copied player/world files
in separate profiles. Set these environment variables before launching each client:

```sh
RIAKAWA_LIVE_COMBAT=/absolute/path/to/output
RIAKAWA_LIVE_PLAYER=/absolute/path/to/copied-player.plr
RIAKAWA_LIVE_WORLD=/absolute/path/to/copied-world.wld
RIAKAWA_QA_CATALOG=/absolute/path/to/assets/vanilla-catalog.json
RIAKAWA_LIVE_CHARACTER=Chiikawa
```

The baseline profile enables only CombatProbe; the comparison profile also enables
Riakawa. Use `Native` for the baseline character. Each weapon runs for 360 native
simulation ticks against active slimes, zombies and cave bats at clear noon. Ticks 1 through 180 receive attack input.
Item checks, projectile updates and native damage rolls receive matching seeds to
remove differences caused by frame timing. Native biome updates also run each tick
so nearby tile buffs do not depend on lighting updates. The client renders between
batches of 256 simulation ticks; every tick is still recorded. Life, mana and ammunition
are not refilled.
The client exits without saving after the last case. Compare the complete outputs:

```sh
python3 checks/LiveCombatChecks.py artifacts/qa/live-day-native \
  artifacts/qa/live-day-chiikawa --full \
  --output artifacts/qa/live-combat-checks.json
```

These replays cover the recorded arena and input sequence. They do not cover every
terrain, accessory, alternate attack or multiplayer timing combination.

The comparison omits four native display/audio slots: one rainbow hue and three
tracked sound handles. [The source review](qa/combat-cosmetic-fields.json) names each
field. Their raw values remain in the traces; all other AI fields are compared.
