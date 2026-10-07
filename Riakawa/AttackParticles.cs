using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Riakawa.Core;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Riakawa;

// Tag particle ownership at the existing attack scopes. Change frames only for
// the draw call: native particle types, AI, lighting, velocities and RNG remain.
public sealed class AttackParticles : ModSystem
{
    private static int[] owners = [];
    private static Texture2D? copiedSource;
    private static int nativeHeight;
    private static bool drawing;
    private static readonly List<(Dust Dust, Rectangle Frame)> restore = [];
    public static int LastStyledCount { get; private set; }

    public override void Load()
    {
        if (Main.dedServ) return;
        owners = new int[Main.dust.Length];
        Array.Fill(owners, -1);
        On_Dust.NewDust += NewDust;
        On_Dust.GetAlpha += GetAlpha;
        On_Main.DrawDust += DrawDust;
    }

    public override void Unload()
    {
        if (Main.dedServ) return;
        On_Dust.NewDust -= NewDust;
        On_Dust.GetAlpha -= GetAlpha;
        On_Main.DrawDust -= DrawDust;
        owners = []; copiedSource = null; restore.Clear(); drawing = false;
        LastStyledCount = 0;
    }

    private static int NewDust(On_Dust.orig_NewDust orig, Vector2 position, int width, int height,
        int type, float speedX, float speedY, int alpha, Color color, float scale)
    {
        int index = orig(position, width, height, type, speedX, speedY, alpha, color, scale);
        if (index >= 0 && index < owners.Length)
            owners[index] = type < DustID.Count ? VisualHooks.EffectOwner : -1;
        return index;
    }

    private static Color GetAlpha(On_Dust.orig_GetAlpha orig, Dust dust, Color color)
    {
        var result = orig(dust, color);
        if (!drawing || dust.type >= DustID.Count || dust.frame.Y < nativeHeight) return result;
        byte brightness = Math.Max(result.R, Math.Max(result.G, result.B));
        return new Color(brightness, brightness, brightness, result.A);
    }

    private static void DrawDust(On_Main.orig_DrawDust orig, Main main)
    {
        LastStyledCount = 0;
        var config = RiakawaConfig.Current;
        if (!config.ReplaceWeapons || !config.Decorations ||
            !CosmeticAssets.TryTexture(CosmeticAssets.Manifest.ParticleAtlas, out var atlas)) {
            orig(main); return;
        }
        var original = TextureAssets.Dust;
        var source = original.Value;
        if (atlas.Value.Width != source.Width || atlas.Value.Height != source.Height + 12) {
            orig(main); return;
        }
        nativeHeight = source.Height;
        if (copiedSource != source) {
            // Preserve unrelated dust pixels exactly; no vanilla atlas is shipped.
            var pixels = new Color[source.Width * source.Height];
            source.GetData(pixels);
            atlas.Value.SetData(0, source.Bounds, pixels, 0, pixels.Length);
            copiedSource = source;
        }
        try {
            for (int i = 0; i < Math.Min(Main.maxDustToDraw, owners.Length); i++) {
                var dust = Main.dust[i];
                int owner = owners[i];
                if (!dust.active || dust.type >= DustID.Count || owner < 0 || owner >= Main.maxPlayers ||
                    !Main.player[owner].active) continue;
                var character = Main.player[owner].GetModPlayer<RiakawaPlayer>().Character;
                if (character == Character.Original) continue;
                restore.Add((dust, dust.frame));
                int column = ((int)character - 1) * 30 + Math.Abs(dust.frame.Y / 10 % 3) * 10;
                // The fourth group is transparent. Draw/culling and AI still run.
                if (config.ReducedEffects && i % 2 != 0) column = 90;
                else LastStyledCount++;
                dust.frame = new Rectangle(column, nativeHeight + 2, 8, 8);
            }
            TextureAssets.Dust = atlas;
            drawing = true;
            orig(main);
        }
        finally {
            drawing = false;
            TextureAssets.Dust = original;
            foreach (var entry in restore) entry.Dust.frame = entry.Frame;
            restore.Clear();
        }
    }
}
