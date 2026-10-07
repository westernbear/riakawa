# Verification

Release results and package hashes are in [RELEASE-REPORT.md](RELEASE-REPORT.md).
The detailed visual review is in [APPEARANCE-REVIEW.md](APPEARANCE-REVIEW.md).
Raw `artifacts/` paths refer to local generated evidence and are excluded from Git.

## Runnable checks

| Check | Command | Coverage |
| --- | --- | --- |
| Core rules | `dotnet run --project checks/CoreChecks.csproj` | Packet bounds, sender checks, character values and emote cooldown |
| Release assets | `python3 scripts/validate-assets.py --release` | Catalog coverage, paths, dimensions, approval records and music mapping; requires Pillow and ffprobe |
| Weapon catalog | `python3 checks/CatalogChecks.py` | Weapons, ammunition and excluded tools; includes the throwable Snowball |
| Artwork | `python3 checks/ArtChecks.py --poses artifacts/qa/momonga/poses` | Texture dimensions, alpha, animation frames and 25,560 native held poses |
| Momonga | `python3 checks/MomongaChecks.py` | Fourth-character assets, native bindings, distinct skins, voice formats and existing-art preservation |
| Recorded attacks | `python3 checks/AppearanceChecks.py` | Standard uses, ammo cases, minions and repaired drawing paths |
| Native defaults | `python3 checks/CombatChecks.py` | Separate runs with and without Riakawa; 5,455 items and 1,021 projectiles |
| Live combat | `python3 checks/LiveCombatChecks.py NATIVE STYLED --full --output REPORT` | Every recorded simulation tick, including damage, timing, movement, collisions and resource payments |
| Multiplayer | `python3 checks/ClientChecks.py` | Saved two-client observations, ownership and late-join state |
| Momonga multiplayer | `python3 checks/MomongaNetworkChecks.py` | Two-client emotes, movement, settings, child ownership and existing summons after reconnect |
| Ammo | `python3 checks/AmmoChecks.py` | 114 native firing cases and child-projectile mappings |
| Voices | `python3 checks/VoiceChecks.py --qa artifacts/qa/momonga --characters Momonga` | Seven two-client scenarios and two captured Momonga voice matches |
| Sounds | `python3 checks/SoundChecks.py` | 52 weapon/instrument cues and twelve voices |
| Music files | `python3 checks/AudioChecks.py` | Twelve decoded loops, clipping, numerical seams and 89 mapped copies |
| Music playback | `python3 checks/MusicChecks.py` | Native selection/fades, nine captured tracks and volume-zero output |

Media checks require ffmpeg; waveform checks also require NumPy. Recorded checks
read their original runs. Passing them again does not create new gameplay evidence.

## Game scenarios

The saved visual and multiplayer runs cover character changes, movement, jumping,
swimming, reversed gravity, wings, mounts, hurt, death, invisibility and emotes.
Armor remains effective while body armor is hidden. Held attacks follow their
owner; inventory and dropped items follow the observer. Original appearance and
individual effect switches restore native drawing and sound behavior.

Two-client scenarios include different characters using the same weapon, existing
summons reaching a late joiner, source inheritance through split projectiles,
remote instruments and per-listener voice settings. Key remapping and intentional
unbinding survive a restart. Dedicated servers load without client media access.

The combat replay uses active native NPC AI in a disposable single-player arena.
The original three characters were compared on version 0.1.0. Momonga uses a separate
0.2.0 replay against the same native baseline: 426 weapons, 360 ticks per weapon
with equal action seeds. No life, mana or ammo
refills are used. See [Development](DEVELOPMENT.md) for the replay setup and the four excluded
display/audio fields. Damage and all remaining AI fields are compared exactly.

The music runs cover day/night, events, ordinary/final bosses, Otherworld, music
boxes and volume changes. The user approved all twelve final tracks and their
loop boundaries; [the approval record](../assets/music-review.json) identifies
the exact source files.

## Limits

Visual discovery sometimes used fixed targets, infinite mana or protected players;
those runs establish drawing coverage. Combat parity uses separate unrestricted
resource and damage updates. Both retain the exact build and scenario in their records.
No finite run covers every terrain, accessory, alternate input, random branch or
combination with other mods. Tests on this headless software renderer do not
establish performance on a typical gaming PC.

Optional GameChecks and CombatProbe mods are excluded from the release. Use copied
saves for their world-editing commands. Workshop verification uses a new profile
with only Riakawa enabled and no local copies of either release package.
