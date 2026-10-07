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
The resulting mod is `dist/Riakawa.tmod`. Rebuild the companion music pack without
regenerating music with:

```sh
python3 scripts/prepare-music.py --pack-only
```

For ordinary C# iteration, pass the installed tModLoader directory directly:

```sh
dotnet build Riakawa/Riakawa.csproj -p:TModLoaderDirectory=/path/to/tModLoader
```

`checks/GameChecks` and `checks/CombatProbe` are optional development mods, excluded
from Riakawa's package. They create isolated test observations; never enable their
world-editing commands in a real save. Recorded QA checks read local `artifacts/qa`
outputs. The published summaries describe the tested versions and limits in
[APPEARANCE-REVIEW.md](APPEARANCE-REVIEW.md).
