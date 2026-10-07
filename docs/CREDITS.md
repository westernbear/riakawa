# Riakawa credits

Riakawa is an unofficial, noncommercial fan project. Chiikawa, Hachiware and Usagi
are characters created by Nagano. Terraria is by Re-Logic; tModLoader is by the
tModLoader contributors. This project is not endorsed by their creators.

## Music

New instrumental music generated for this project with
[YuE2-3B](https://huggingface.co/m-a-p/YuE2-3B) and
[YuE2-Vae](https://huggingface.co/m-a-p/YuE2-Vae), by the YuE2 / m-a-p team.
The published checkpoints use [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/).
Source: [YuE2 paper](https://arxiv.org/abs/2609.33757).
Riakawa contributors provided prompts and performed loop crossfades, volume
normalization and Vorbis encoding. Generated audio is new; it does not use anime
recordings or the original Terraria soundtrack. The project and supplied music
pack are distributed for noncommercial use.

Generation settings, model file hashes and processing records are retained with
the development sources. The user approved all twelve final tracks and their loops
on 2026-10-07; the exact audio hashes are in `assets/music-review.json`.

## Artwork and voices

SDXL + Pixel Art XL trials were rejected and are not included in release assets.
[Z-Image-Turbo](https://huggingface.co/Tongyi-MAI/Z-Image-Turbo) (Apache 2.0) was tested
for reference art; its samples were rejected for likeness.
[Official character reference](https://www.anime-chiikawa.jp/chara.html):
© Nagano / Chiikawa Production Committee. Used to guide pixel silhouette and
face studies; original reference files are not included in the mod package.
The batch weapon, projectile and secondary-effect artwork adapts Terraria's installed weapon silhouettes, by
Re-Logic, with new palettes and character ornaments. These are derivative fan
skins; the five initial weapon sets were drawn separately. Native reference
exports are retained locally for alignment and are not shipped as original files.
[Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS) (Apache 2.0) was used for three
original emote voices and six hurt/death cues. The Base model reuses this project's
synthetic VoiceDesign emotes to keep the character voices consistent. No actor
recording is used. Model revisions, seeds, reference hashes and the generation
environment are recorded with the source samples; outer silence is trimmed and
the cues are normalized to mono PCM16 at 44.1 kHz.

[JavaScript sfxr](https://github.com/chr15m/jsfxr) 1.4.1, by Eric Fredricksen
and Chris McCormick (Unlicense), generated 39 short sounds: 21 character/weapon-family effects and 18 three-note instrument chords.

## Korean lettering

The small Hangul glyph atlases are rasterized from WenQuanYi Zen Hei, by the
WenQuanYi contributors. Its GPL-2.0 font embedding exception and M+ font terms
are preserved in `Riakawa/Assets/Fonts/WenQuanYi-LICENSE.txt` and `GPL-2.txt` alongside the atlases.
Only glyphs used by Riakawa are supplied; Terraria's existing glyphs are retained.
