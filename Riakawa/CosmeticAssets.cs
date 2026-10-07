using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Riakawa.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace Riakawa;

public sealed class AnimationSpec
{
    public int Row { get; set; }
    public int Frames { get; set; } = 1;
    public int Ticks { get; set; } = 8;
}

public sealed class PlayerArt
{
    public Character Character { get; set; }
    public string Status { get; set; } = "pending";
    public string? Texture { get; set; }
    public int FrameWidth { get; set; } = 40;
    public int FrameHeight { get; set; } = 56;
    public int[] Feet { get; set; } = [20, 50];
    public int[] Head { get; set; } = [6, 2, 28, 28];
    public Dictionary<string, AnimationSpec> Animations { get; set; } = [];
    public Dictionary<string, string> Voices { get; set; } = [];
}

public sealed class TextureBinding
{
    public string Slot { get; set; } = "Projectile";
    public int Id { get; set; }
    public string Texture { get; set; } = "";
}

public sealed class AttackArt
{
    public int ProjectileId { get; set; }
    public string? Texture { get; set; }
    public List<TextureBinding> Bindings { get; set; } = [];
    public Dictionary<string, string> Sounds { get; set; } = [];
}

public sealed class WeaponArt
{
    public int ItemId { get; set; }
    public Character Character { get; set; }
    public string Status { get; set; } = "pending";
    public bool CoverageReviewed { get; set; }
    public string? Texture { get; set; }
    public string? UseSound { get; set; }
    public Dictionary<string, string> Sounds { get; set; } = [];
    public List<TextureBinding> HeldBindings { get; set; } = [];
    public List<AttackArt> Attacks { get; set; } = [];
}

public sealed class AssetManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string TModLoader { get; set; } = "2026.08.3.0";
    public string? ParticleAtlas { get; set; }
    public List<PlayerArt> Players { get; set; } = [];
    public List<WeaponArt> Weapons { get; set; } = [];
}

public sealed class CosmeticAssets : ModSystem
{
    internal static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };
    internal static AssetManifest Manifest = new();
    private static readonly Dictionary<Character, PlayerArt> Players = [];
    private static readonly Dictionary<(Character, int), WeaponArt> Weapons = [];
    private static readonly Dictionary<string, Asset<Texture2D>> Textures = [];
    private static readonly HashSet<string> Missing = [];

    public override void Load()
    {
        Manifest = JsonSerializer.Deserialize<AssetManifest>(Mod.GetFileBytes("Assets/manifest.json"), JsonOptions)
            ?? throw new InvalidOperationException("Riakawa asset manifest is empty.");
        if (Manifest.SchemaVersion != 1) throw new InvalidOperationException("Unsupported Riakawa asset manifest.");
        foreach (var art in Manifest.Players) Players.Add(art.Character, art);
        foreach (var art in Manifest.Weapons) Weapons.Add((art.Character, art.ItemId), art);
    }

    public override void Unload()
    {
        Players.Clear(); Weapons.Clear(); Textures.Clear(); Missing.Clear(); Manifest = new();
    }

    internal static bool TryTexture(string? path, out Asset<Texture2D> texture)
    {
        texture = null!;
        if (Main.dedServ || string.IsNullOrEmpty(path) || Missing.Contains(path)) return false;
        if (Textures.TryGetValue(path, out texture!)) return true;
        if (!ModContent.RequestIfExists<Texture2D>("Riakawa/" + path, out texture!, AssetRequestMode.ImmediateLoad)) {
            Missing.Add(path);
            return false;
        }
        Textures.Add(path, texture);
        return true;
    }

    internal static bool TryPlayer(Character character, out PlayerArt art, out Asset<Texture2D> texture)
    {
        texture = null!;
        return Players.TryGetValue(character, out art!) && TryTexture(art.Texture, out texture);
    }

    internal static WeaponArt? Weapon(Character character, int itemId)
        => Weapons.GetValueOrDefault((character, itemId));

    internal static bool HasVoice(Character character, string cue)
        => !Main.dedServ && RiakawaConfig.Current.Voices && Players.TryGetValue(character, out var art)
            && art.Voices.TryGetValue(cue, out var path) && HasSound(path);

    internal static bool HasSound(string? path) => !string.IsNullOrEmpty(path) &&
        (ModContent.GetInstance<Riakawa>().FileExists(path + ".ogg") ||
         ModContent.GetInstance<Riakawa>().FileExists(path + ".wav"));

    internal static SoundStyle Sound(string path) => new("Riakawa/" + path) {
        MaxInstances = 3, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew, Volume = 0.65f
    };

    internal static string SoundKey(SoundStyle style) => style.SoundPath +
        (style.Variants.IsEmpty ? "" : ":" + string.Join(",", style.Variants.ToArray()));

    internal static void PlayVoice(Character character, string cue, Vector2 position)
    {
        if (HasVoice(character, cue)) SoundEngine.PlaySound(Sound(Players[character].Voices[cue]), position);
    }
}
