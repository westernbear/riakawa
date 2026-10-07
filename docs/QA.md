# Latest appearance review

See [APPEARANCE-REVIEW.md](APPEARANCE-REVIEW.md) for the new checks and their limits.
`artifacts/` paths below refer to generated local evidence, excluded from Git.

# Release verification

Record the build hash and exact scenario. Draft screenshots and saved observations
are not a full-release pass. Optional `checks/GameChecks` never ships in the mod.

## Automated

- [x] Packet bounds, sender spoofing, enums, duration and emote cooldown.
- [x] C# compilation with zero warnings/errors; package creation succeeds.
  Optional small-icon packaging is excluded; the main icon is retained.
- [x] Dedicated server load without client texture/audio access.
- [x] Runtime catalog and native texture dimensions; draft validation.
- [x] Throwable-ammo boundary regression: `python3 checks/CatalogChecks.py`.
- [x] 114 native ammo firing cases and shared child mappings: `python3 checks/AmmoChecks.py`.
- [x] Otherworld/menu coverage and rain/wind exclusions.
- [x] Twelve decoded loops: no clipping, numerical seam < 0.005; 89 copies match.
  Run `FFMPEG=/usr/bin/ffmpeg python3 checks/AudioChecks.py`.
- [x] 39 bounded PCM effect drafts plus nine distinct voices: `python3 checks/SoundChecks.py`.
- [x] All native item/projectile defaults against Riakawa-free baseline: `python3 checks/CombatChecks.py`.
- [x] All 2,905 packaged textures, 48 sound cues and 1,278 owner/observer draw contracts load in FNA.
- [x] Transparent/glow art regression: `.tools/assets-env/bin/python scripts/draw-weapon-batch.py --self-check`.
- [x] Saved two-client assertions: `python3 checks/ClientChecks.py`.
- [ ] All assets pass `scripts/validate-assets.py --release`.
- [ ] Every individual design, attack variant, sound and binding reviewed.

## Real game evidence (development builds)

- [x] Real Steam-authenticated client load and separate two-client connection.
- [x] Korean configuration/tooltip glyphs and Korean/English emote text.
- [x] Chiikawa/Hachiware/Usagi sheets, movement/jump and custom death observed.
- [x] Distinct characters, remote emotes, parent/child weapon provenance.
- [x] Copper Shortsword design differs per observer; native 5 damage/13 use ticks.
- [x] Slime Staff minion owner/source and 8 damage, 24×16 hitbox on both clients.
- [x] Prism holdout and six beams retain the Last Prism source on both clients.
- [x] Original restoration/Hachiware switch affects existing minions and remote view.
- [x] Remapping to B and deliberate unbinding survive restarts.
- [x] Music pack activation and replacement OGGAudioTrack selected in game.
- [x] Matching live states/screenshots for water and inverted gravity; wings, Slime Mount and armor defense observed.
- [ ] Every direction/action/equipment combination; dyes, lighting and shadows.
- [x] Body armor hidden; native wings/mounts retained; invisibility and custom death observed.
- [x] Effect reduction/disable and weapon disable restore shared dust assets.
- [x] Moving/attacking emote sync; pre-existing Slime Staff summon reaches a late joiner.
- [x] SFX toggle returns native sound while weapon visuals remain enabled.
- [x] Receiver-local voice toggle, Original fallback and intentionally silent hurt: 15 two-client cases.
  Six captured outputs match shipped cues: `python3 checks/VoiceChecks.py`.
- [x] Clean profile with only Riakawa: default Chiikawa/V, native config save and
  process-restart persistence for Korean/Hachiware/voices-off. Imported QA player/world.
- [ ] Every reconnect/equipment combination.
- [ ] Dropped items use observer; held attacks use owner across all weapon families.
- [x] Four real-use comparisons (Original/Chiikawa): weapon damage/use duration,
  arrow and Snowball consumption; Amethyst Staff pays 5 mana in both modes.
  Observed arrow/staff projectile damage and hitboxes match. Run `python3 checks/UseChecks.py`.
- [ ] Full vanilla comparison: every weapon, timing/range/collision/knockback and resource costs.
- [x] Native NPC hit and unowned item cues remain native while weapon cues change.
- [x] Remote Stellar Tune chords follow the owner's character.
- [ ] Listening under many simultaneous attacks.
- [x] Native fades and selection: day/night, Pumpkin Moon, Eye of Cthulhu, Moon Lord,
  Otherworld day/night/final, native music box and volume-zero output. Nine captured
  tracks identified in the actual output (`python3 checks/MusicChecks.py`).
- [ ] Listening: remaining emotes/effects, repeated loops, events/bosses, music boxes, Otherworld,
  volumes. Daytime output correlation 0.9964 and emote correlation 0.9777 verify
  playback only; run `python3 checks/PlaybackChecks.py`.
- [x] Current development package/server/two-client load and crystal-child sync.
- [x] Current preview clean local install/server/reconnect without QA helper mods.
- [ ] Exact approved release and fresh Workshop subscription installation.

Native discovery completed the original 425 weapon attempts and 38 single-cast summon tests;
manual Mace, Snowball, alternate-use and full-charge observations add missing branches.
The 114 representative ammo cases all produced observed projectiles. Explosive
self-damage caused respawn pauses, and cleanup kills also discover native children;
this is not a survivability or full-combination comparison. Shared child/type
bindings are inferred across weapons and still require individual review.
Tiger ranks 1–3 and Abigail retain their staff after switching the held item;
armor-created Stardust Guardian remains native. These runs are discovery, not
combat-parity passes. Shader trail textures were observed for six native drawers;
all 21 Zenith sword profiles were audited against the pinned source. Actual native
Apply calls received custom images in all six drawers; custom Tiger also reached a
late joiner.

Build hashes and scenario records live under `artifacts/qa`: `network-build.json`,
`special-build.json`, `integration-build.json`, `tiered-build.json`, `shader-build.json`
`final-build.json`, `current-build.json`, `mana-build.json`, `ammo-build.json`
and `preview-build.json`. Startup checks cover sound settings/cache, nested NPC
scope, remote instrument bounds, tiered summon ownership and shader restoration.
The deliberate shader-failure check emits an expected caught exception in the log.
The safe QA world suppresses hostile NPCs and fixes weather/time; it is for visual
staging only. Publication requires all remaining checks plus credits, support
version, screenshots and reciprocal links between Workshop entries.
