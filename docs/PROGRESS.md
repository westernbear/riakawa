# Riakawa progress — 2026-10-07

Latest: [full appearance/attack technical review](APPEARANCE-REVIEW.md).
426 standard-use cases, 114 ammo cases, 38 single-cast summons and 20,448 native
held poses reviewed; missing chains, glows, Flairon bubbles and moving attack body
frames repaired. README screenshots come from the running game.
Production approval flags remain separate from these bounded technical checks.


## Scope and approvals

Noncommercial fan project: three characters, three distinct designs for every
vanilla weapon and its attacks, character voices/SFX, twelve music themes and
linked Workshop releases. The full scope has not been reduced.

The user approved the YuE2 daytime sample's mood, the cleaned character pixels
(“이 도트로 진행”), and the special-weapon design direction
(“이 방향으로 나머지 무기도 진행”). Records: `assets/art-review.json`.
The six hurt/death voice samples were approved in direction (“현재 방향 유지”).
Final animation, individual weapon/attack and remaining audio reviews remain. README is kept
short. PixelLab/Ludo subscriptions are unnecessary.

## Current implementation

- tModLoader v2026.08.3.0 / Terraria 1.4.4.9, SDK 8.0.425.
  Source: c9545cf27d56c456321e1896274c02adf67d0140.
  Archive SHA256: 61e865f3702b12ce4a26c5a90b9de99a12c65ffc228455eb3390ce54af1eab15.
- Config/restoration, player drawing, emotes, bounded/rate-limited networking,
  projectile parent provenance, scoped texture swaps and source-specific sound hooks.
- Three player sheets, each 160×504: idle, walk, jump, swim, attack, mount, hurt,
  death, emote. Reference pixels approved; animation review remains.
- Korean/English mod labels and tooltips. The pinned game has no Korean culture;
  only Riakawa text and missing Hangul glyphs are added. Font licenses are packaged.
- Default V initialized once per profile; remaps and deliberate unbinding preserved.
  Custom-character death hides vanilla body gore; invisibility keeps body layers hidden.
- **1,278/1,278 item texture drafts connected**, for 426 weapon candidates.
  Copper Shortsword, Wooden Bow, Slime Staff, Leather Whip and Last Prism have
  separately drawn designs; the other 421 use installed silhouettes redrawn in
  character palettes with different ribbon/fish/ear ornaments. They retain native
  canvas dimensions and handle locations. This is draft coverage, not release approval.
- 1,413 generated attack sheet drafts are linked, preserving native canvas and alpha.
  Runtime PickAmmo resolved 1,109 weapon/ammo combinations. Native-use exploration
  found additional swords, splits, minion variants and healing projectiles; 192 secondary sheets cover observed chains, glows and native shader trails.
  All 21 Zenith sword profiles are linked. A 114-case native ammo pass
  found additional splits/fragments. Observed child-type closure, secondary
  bindings and attack sounds are shared across weapons using those projectiles;
  these inferred mappings remain subject to review.
- Source-owned particle cells are swapped only during drawing, then frames and the
  shared dust atlas are restored. Native types, AI, light and random draws are retained.
- Nine normalized voice cues (three emotes and six hurt/death cues) plus 39 procedural weapon/instrument effect drafts.
  All 1,206 weapon/character entries with native use sounds are mapped; secondary
  cues include instrument chords and remote instrument packets. Loop/pause/volume/pitch behavior is retained.
  The six new cues use pinned Qwen3-TTS Base with this project’s synthetic emote references.
  Their direction is user-approved and all six passed local/remote playback checks.
- Twelve draft music loops, 89 vanilla/Otherworld mappings. Native selection stays
  intact; rain/wind ambience 28/45 is excluded. Pack includes a pixel icon.
  `python3 scripts/prepare-music.py --pack-only` rebuilds the ZIP without generation.

## Observed verification

Current release-audit details and exact build hashes: [RELEASE-REPORT.md](RELEASE-REPORT.md).

- All 15 two-client hurt/death cases pass: receiver-local voice preferences, native
  Original fallback, independent SFX toggle and intentional silent hurt. The
  networked HurtInfo damage, knockback and sound flag are unchanged. Six actual
  output captures match the packaged voice waveforms (`checks/VoiceChecks.py`).
- The exact package loads 2,905 textures and 48 sounds through FNA, and all 1,278
  weapon/character owner/observer draw contracts pass offscreen. This does not
  establish every live handle offset or projectile animation.
- Static contact sheets of all weapon textures, all player frames, 477 primary
  projectile types and 87 secondary binding groups were visually inspected.
  Scope and limitations: `artifacts/qa/release-art/review.json`.
- Isolated Riakawa-free/Riakawa runs with identical per-type random seeds have
  identical defaults for all 5,455 native items and 1,021 projectiles, including
  all 426 weapon candidates (`checks/CombatChecks.py`). Live parity remains separate.

- Core packet/rate checks, draft asset validation, C# build/package pass.
  C# compilation and packaging have zero warnings/errors. The optional small icon is
  excluded to avoid an upstream conversion bug; the main icon is retained. Release
  validation fails on 1,294 unreviewed scope/art/music entries, not missing voice files.
- Dedicated server loads without client media access.
- Two real Steam clients on Xvfb :100/:101, software OpenGL: distinct characters,
  localized V emotes, parent/child provenance, minion ownership, prism/six beams,
  Original/Hachiware changes and existing summon appearance were observed.
- Remapped B and deliberate unbinding survived restarts. Saved observations have
  runnable assertions: `python3 checks/ClientChecks.py`. Build identities are in
  `artifacts/qa/network-build.json`, `special-build.json` and `preview-build.json`.
- Screens under `artifacts/qa`: Korean config/tooltips, custom death, whip/minion,
  prism beams, original restoration, Hachiware/Usagi and observer views.
- Music pack activates; runtime selects `OGGAudioTrack` for replaced music.
  Ten music cases pass native selection/fades, nine recorded track identities and
  volume-zero silence: day/night, event, ordinary/final boss, Otherworld day/night/
  final and native title music box (`checks/MusicChecks.py`). Boss AI was frozen.
  The mixed recordings do not establish uninterrupted playback or listening quality.
  Twelve loops decode without clipping, numerical seam discontinuity < 0.005,
  and all 89 copies match their source loops. Captured system-output waveform
  correlation after enabling frame skipping is 0.9964 for the actual daytime track.
  Captured emote correlation is 0.9777 after common-rate decoding. These verify
  playback, not listening quality or transitions: `python3 checks/PlaybackChecks.py`.
- Native Heat Ray styled 249 particles in a recorded frame; atlas/frame restoration passed.
  Iron armor stays functional (defense 9); wings, Slime Mount, invisibility and custom
  death were observed. Native water and inverted gravity now have matching living-state snapshots/screenshots.
- First broad use sweep covered 361/425 weapons before the isolated server spam
  threshold disconnected it. Records are in `artifacts/qa/first-sweep`; DD2 sentries
  needed progression unlocked. The corrected native audit completed all 425 weapon attempts; a separate
  single-cast pass completed 38 summon weapons. Manual Mace, alternate-use, full
  charge and tiered-summon passes extend discovery. These are not combat-parity
  tests (frozen enemy/infinite mana in the sweeps/cleanup).
- Late join preserves pre-existing Slime Staff ownership. Remote Stellar Tune
  playback uses the owner's character. NPC hit and unowned item sounds remain native.
  Effect toggles and moving/attacking emotes passed saved-state checks.
- Fixed Desert Tiger/Abigail body provenance: native Misc sources inherit the
  owner's invisible staff counters, including all three tiger ranks after changing
  the held weapon. Armor-spawned Stardust Guardian remains unmodified.
- Native trail shaders use the same scoped bindings as sprites. Startup checks
  verify shared images restore after normal and deliberately failed draws. The
  deliberate QA exception is expected in the log; it is not a gameplay failure.
- Scope audit recovered the directly throwable Snowball, which had been incorrectly
  excluded as ammunition. The catalog now has 426 weapon candidates;
  `python3 checks/CatalogChecks.py` protects the weapon/ammunition/tool boundaries.
- Four real-use Original/Chiikawa comparisons preserve observed damage/use
  duration, arrows/Snowball consumption and actual staff mana payment. This is
  limited evidence, not full range/collision/knockback parity: `checks/UseChecks.py`.
- Ammo discovery completed 114 representative native firing cases for the
  1,109 PickAmmo combinations, with projectiles observed in every case. Some
  explosive cases killed the QA player; the sweep paused during respawn and
  resumed. This is not a balance/survivability pass. Known splits/fragments
  have a runnable mapping check: `python3 checks/AmmoChecks.py`.
- Clean local profiles with only Riakawa and its music ZIP passed native server/client
  entry, default Chiikawa/V, and Korean/Hachiware/voices-off persistence after full
  process restart. Imported QA player/world; Workshop subscription remains untested.
- The current preview loaded on both real clients. Both observed the same crystal
  bullet child with its original firing weapon/owner. Consolidated status and
  package hashes: `artifacts/verification-current.json`. No Workshop item is posted.
- Catalog: 426 weapon candidates, 1,021 projectile types, 7,972 initial texture dimensions (binding exports add scalar slots).
  Scope and special attacks remain unreviewed. No asset is marked release-approved.

## Remaining before publication

1. Produce/audit every associated attack, ammo variant, secondary texture and SFX.
2. Review all individual designs, player actions, equipment and configuration states.
3. Compare actual damage, timing, range, collisions, ammo/mana and knockback against
   vanilla. Prototype field observations do not establish full combat parity.
4. Complete musical loop/listening and crowded-attack listening reviews. The six
   hurt/death cues already have user direction approval.
5. Pass release gate, final dedicated and clean-install/subscription checks;
   prepare reciprocal Workshop links/screenshots/credits and publish both items.

## Environment

GPU 54550950 was **destroyed** after all six hurt/death outputs and metadata
were hash-verified locally (`artifacts/vast-voices-destroyed.json`).
GPU 54477542 was **destroyed**, as explicitly requested, and verified absent.
All 19 selected outputs (12 music, 3 voices, 4 images) were hash-verified locally
first. The stop watchdog was removed. Evidence: `artifacts/vast-destroyed.json`.
Earlier instance 54476992 was also destroyed. Do not alter unrelated instances.

`checks/GameChecks` is an optional development mod, never part of the release.
`RIAKAWA_QA_SAFE=1` on the isolated QA server holds clear noon, removes nearby test
gravestones and suppresses hostile spawns. This stages visuals, not combat balance.
Test players/loadouts live in `.tools`.

The human browser on :99 remains handed off; do not inspect credentials/cookies.
Native Steam login on :100 was confirmed by the user.
