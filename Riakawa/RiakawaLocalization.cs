using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Riakawa;

// tML 1.4.4 has no ko-KR culture. Override only this mod's registered text and
// add missing Hangul glyphs; the game's selected language and fonts stay intact.
public sealed class RiakawaLocalization : ModSystem
{
    private static readonly Dictionary<string, string> Korean = [];
    private static readonly Dictionary<string, string> Defaults = [];
    private static readonly List<(DynamicSpriteFont Font, char Character, Texture2D Texture)> AddedGlyphs = [];
    private static readonly MethodInfo SetValue = typeof(LocalizedText).GetMethod("SetValue",
        BindingFlags.Instance | BindingFlags.NonPublic)!;

    public override void Load()
    {
        if (Main.dedServ) return;
        using var json = JsonDocument.Parse(Mod.GetFileBytes("Localization/ko-KR.hjson"));
        Flatten(json.RootElement, "");
    }

    private static void Flatten(JsonElement value, string prefix)
    {
        foreach (var field in value.EnumerateObject()) {
            string key = prefix.Length == 0 ? field.Name : prefix + "." + field.Name;
            if (field.Value.ValueKind == JsonValueKind.Object) Flatten(field.Value, key);
            else if (field.Value.ValueKind == JsonValueKind.String && key.StartsWith("Mods.Riakawa."))
                Korean.Add(key, field.Value.GetString()!);
        }
    }

    public override void OnLocalizationsLoaded()
    {
        if (Main.dedServ) return;
        foreach (var key in Korean.Keys) Defaults[key] = Language.GetTextValue(key);
        InstallGlyphs();
        Apply();
    }

    internal static void Apply()
    {
        if (Main.dedServ || Defaults.Count == 0) return;
        var text = RiakawaConfig.Current.InterfaceLanguage == InterfaceLanguage.Korean ? Korean : Defaults;
        foreach (var (key, value) in text) SetValue.Invoke(Language.GetText(key), [value]);
    }

    private void InstallGlyphs()
    {
        using var json = JsonDocument.Parse(Mod.GetFileBytes("Assets/Fonts/korean.json"));
        string characters = json.RootElement.GetProperty("characters").GetString()!;
        var fonts = new[] { FontAssets.MouseText, FontAssets.DeathText, FontAssets.ItemStack }
            .Concat(FontAssets.CombatText);
        foreach (var asset in fonts) {
            if (asset == null) continue;
            var font = asset.Value;
            int size = font.LineSpacing <= 24 ? 16 : font.LineSpacing <= 34 ? 22 : 32;
            if (!CosmeticAssets.TryTexture($"Assets/Fonts/korean-{size}", out var atlas)) continue;
            int cell = size + 4;
            for (int i = 0; i < characters.Length; i++) {
                char character = characters[i];
                if (font.IsCharacterSupported(character)) continue;
                var glyph = new Rectangle(i % 16 * cell, i / 16 * cell, cell, cell);
                var padding = new Rectangle(-2, Math.Max(0, (font.LineSpacing - size) / 2) - 2, size, font.LineSpacing);
                font.SpriteCharacters.Add(character, new(atlas.Value, glyph, padding, new Vector3(0, size, 0)));
                AddedGlyphs.Add((font, character, atlas.Value));
            }
        }
    }

    public override void Unload()
    {
        foreach (var (font, character, texture) in AddedGlyphs)
            if (font.SpriteCharacters.TryGetValue(character, out var data) && data.Texture == texture)
                font.SpriteCharacters.Remove(character);
        AddedGlyphs.Clear(); Korean.Clear(); Defaults.Clear();
    }
}
