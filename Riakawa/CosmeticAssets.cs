using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Riakawa.Core;
using Terraria;
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
    public string? Texture { get; set; }
    public int FrameWidth { get; set; } = 40;
    public int FrameHeight { get; set; } = 56;
    public int[] Feet { get; set; } = [20, 50];
    public int[] Head { get; set; } = [6, 2, 28, 28];
    public Dictionary<string, AnimationSpec> Animations { get; set; } = [];
}

public sealed class AssetManifest
{
    public int SchemaVersion { get; set; } = 1;
    public List<PlayerArt> Players { get; set; } = [];
}

public sealed class CosmeticAssets : ModSystem
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() }
    };
    private static readonly Dictionary<Character, PlayerArt> Players = [];
    private static readonly Dictionary<string, Asset<Texture2D>> Textures = [];

    public override void Load()
    {
        var manifest = JsonSerializer.Deserialize<AssetManifest>(Mod.GetFileBytes("Assets/manifest.json"), JsonOptions)
            ?? throw new InvalidOperationException("Riakawa asset manifest is empty.");
        if (manifest.SchemaVersion != 1) throw new InvalidOperationException("Unsupported Riakawa asset manifest.");
        foreach (var art in manifest.Players) Players.Add(art.Character, art);
    }

    public override void Unload()
    {
        Players.Clear();
        Textures.Clear();
    }

    internal static bool TryTexture(string? path, out Asset<Texture2D> texture)
    {
        texture = null!;
        if (Main.dedServ || string.IsNullOrEmpty(path)) return false;
        if (Textures.TryGetValue(path, out texture!)) return true;
        if (!ModContent.RequestIfExists<Texture2D>("Riakawa/" + path, out texture!, AssetRequestMode.ImmediateLoad))
            return false;
        Textures.Add(path, texture);
        return true;
    }

    internal static bool TryPlayer(Character character, out PlayerArt art, out Asset<Texture2D> texture)
    {
        texture = null!;
        return Players.TryGetValue(character, out art!) && TryTexture(art.Texture, out texture);
    }
}
