using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Content;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace Riakawa;

// Scope texture changes to one vanilla draw call. Vanilla retains whip curves,
// spear origins, beam segments and afterimages; finally restores shared assets.
public sealed class VisualHooks : ModSystem
{
    [ThreadStatic] private static Player? soundPlayer;
    [ThreadStatic] private static Projectile? soundProjectile;
    [ThreadStatic] private static Dictionary<string, Asset<Texture2D>>? shaderTextures;
    private static readonly FieldInfo[] ShaderImages = typeof(MiscShaderData)
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .Where(f => f.FieldType == typeof(Asset<Texture2D>)).ToArray();
    internal static int EffectOwner => soundProjectile is { } projectile ?
        AttackOrigin.Art(projectile) != null ? projectile.owner : -1 :
        soundPlayer is { } player && CosmeticAssets.Weapon(player.GetModPlayer<RiakawaPlayer>().Character,
            player.HeldItem.type) != null ? player.whoAmI : -1;
    private static readonly Dictionary<string, FieldInfo> Slots = typeof(TextureAssets)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(Asset<Texture2D>) || f.FieldType == typeof(Asset<Texture2D>[]))
        .ToDictionary(f => f.Name);

    public override void Load()
    {
        if (Main.dedServ) return;
        On_Main.DrawProjDirect += DrawProjectile;
        IL_Player.Hurt_HurtInfo_bool += HurtSound;
        On_MiscShaderData.Apply += ApplyShader;
        On_Player.ItemCheck += ItemCheck;
        On_Projectile.AI += ProjectileAI;
        On_Projectile.Kill += ProjectileKill;
        On_NPC.StrikeNPC_HitInfo_bool_bool += StrikeNpc;
        On_NPC.HitEffect_HitInfo += NpcHitEffect;
        On_MessageBuffer.GetData += InstrumentPacket;
        On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback += PlaySound;
    }

    public override void Unload()
    {
        if (Main.dedServ) return;
        On_Main.DrawProjDirect -= DrawProjectile;
        IL_Player.Hurt_HurtInfo_bool -= HurtSound;
        On_MiscShaderData.Apply -= ApplyShader;
        On_Player.ItemCheck -= ItemCheck;
        On_Projectile.AI -= ProjectileAI;
        On_Projectile.Kill -= ProjectileKill;
        On_NPC.StrikeNPC_HitInfo_bool_bool -= StrikeNpc;
        On_NPC.HitEffect_HitInfo -= NpcHitEffect;
        On_MessageBuffer.GetData -= InstrumentPacket;
        On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback -= PlaySound;
        soundPlayer = null; soundProjectile = null;
        shaderTextures = null;
    }

    // SoundDisabled is transmitted with HurtInfo. Suppress only the receiver's
    // playback branch, after networking, so each listener retains their toggle.
    private static void HurtSound(ILContext il)
    {
        var cursor = new ILCursor(il);
        cursor.GotoNext(MoveType.After, i => i.MatchLdfld<Player.HurtInfo>(nameof(Player.HurtInfo.SoundDisabled)));
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<bool, Player, bool>>((disabled, player) => disabled ||
            CosmeticAssets.HasVoice(player.GetModPlayer<RiakawaPlayer>().Character, "hurt"));
    }

    private static void DrawProjectile(On_Main.orig_DrawProjDirect orig, Main main, Projectile projectile)
    {
        var attack = RiakawaConfig.Current.ReplaceWeapons ?
            AttackOrigin.Art(projectile)?.Attacks.Find(a => a.ProjectileId == projectile.type) : null;
        if (attack == null) { orig(main, projectile); return; }
        List<Action> restore = [];
        var previousTextures = shaderTextures;
        shaderTextures = [];
        try {
            Main.instance.LoadProjectile(projectile.type);
            Swap("Projectile", projectile.type, attack.Texture, restore);
            foreach (var binding in attack.Bindings) Swap(binding.Slot, binding.Id, binding.Texture, restore);
            orig(main, projectile);
        }
        finally {
            for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
            shaderTextures = previousTextures;
        }
    }

    private static void Swap(string slot, int id, string? path, List<Action> restore)
    {
        if (!Slots.TryGetValue(slot, out var field) || !CosmeticAssets.TryTexture(path, out var replacement)) return;
        if (field.GetValue(null) is Asset<Texture2D>[] array && id >= 0 && id < array.Length) {
            // Secondary item/projectile sheets may first load inside the native
            // draw. Load them before the swap to cover that first frame too.
            if (slot == "Item") Main.instance.LoadItem(id);
            else if (slot == "Projectile") Main.instance.LoadProjectile(id);
            var original = array[id];
            if (original?.Value == null || original.Value.Bounds != replacement.Value.Bounds) return;
            shaderTextures![original.Name] = replacement;
            array[id] = replacement;
            restore.Add(() => array[id] = original);
        }
        else if (field.GetValue(null) is Asset<Texture2D> original) {
            if (original.Value.Bounds != replacement.Value.Bounds) return;
            shaderTextures![original.Name] = replacement;
            field.SetValue(null, replacement);
            restore.Add(() => field.SetValue(null, original));
        }
    }

    // Native vertex-strip trails request cached assets by path, bypassing
    // TextureAssets. Use the same manifest bindings only for this draw scope.
    private static void ApplyShader(On_MiscShaderData.orig_Apply orig, MiscShaderData shader, DrawData? drawData)
    {
        if (shaderTextures == null || shaderTextures.Count == 0) { orig(shader, drawData); return; }
        List<(FieldInfo Field, Asset<Texture2D> Original)> restore = [];
        try {
            foreach (var field in ShaderImages)
                if (field.GetValue(shader) is Asset<Texture2D> original &&
                    shaderTextures.TryGetValue(original.Name, out var replacement)) {
                    restore.Add((field, original));
                    field.SetValue(shader, replacement);
                }
            orig(shader, drawData);
        }
        finally { foreach (var entry in restore) entry.Field.SetValue(shader, entry.Original); }
    }

    private static void ItemCheck(On_Player.orig_ItemCheck orig, Player player)
    {
        var previousPlayer = soundPlayer;
        var previousProjectile = soundProjectile;
        soundPlayer = player; soundProjectile = null;
        try { orig(player); }
        finally { soundPlayer = previousPlayer; soundProjectile = previousProjectile; }
    }

    private static void ProjectileAI(On_Projectile.orig_AI orig, Projectile projectile)
    {
        var previous = soundProjectile;
        soundProjectile = projectile;
        try { orig(projectile); }
        finally { soundProjectile = previous; }
    }

    private static void ProjectileKill(On_Projectile.orig_Kill orig, Projectile projectile)
    {
        var previous = soundProjectile;
        soundProjectile = projectile;
        try { orig(projectile); }
        finally { soundProjectile = previous; }
    }

    // An attack may synchronously enter NPC damage/effect code. Its blood and
    // voice belong to that NPC, even inside our outer item/projectile scope.
    private static int StrikeNpc(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC npc,
        NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
    {
        var previous = (soundPlayer, soundProjectile);
        soundPlayer = null; soundProjectile = null;
        try { return orig(npc, hit, fromNet, noPlayerInteraction); }
        finally { (soundPlayer, soundProjectile) = previous; }
    }

    private static void NpcHitEffect(On_NPC.orig_HitEffect_HitInfo orig, NPC npc, NPC.HitInfo hit)
    {
        var previous = (soundPlayer, soundProjectile);
        soundPlayer = null; soundProjectile = null;
        try { orig(npc, hit); }
        finally { (soundPlayer, soundProjectile) = previous; }
    }

    // Remote harp/guitar notes play from vanilla packet 58, outside ItemCheck.
    // Read only the bounded player byte; leave parsing/validation to vanilla.
    private static void InstrumentPacket(On_MessageBuffer.orig_GetData orig, MessageBuffer buffer,
        int start, int length, out int messageType)
    {
        var previous = (soundPlayer, soundProjectile);
        if (Main.netMode == NetmodeID.MultiplayerClient && start >= 0 && length == 6 &&
            (long)start + length <= buffer.readBuffer.Length && buffer.readBuffer[start] == MessageID.InstrumentSound &&
            buffer.readBuffer[start+1] < Main.maxPlayers && Main.player[buffer.readBuffer[start+1]].active) {
            soundPlayer = Main.player[buffer.readBuffer[start+1]];
            soundProjectile = null;
        }
        try { orig(buffer, start, length, out messageType); }
        finally { (soundPlayer, soundProjectile) = previous; }
    }

    private static SlotId PlaySound(On_SoundEngine.orig_PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback orig,
        ref SoundStyle style, Vector2? position, SoundUpdateCallback callback)
    {
        string? replacement = null;
        if (RiakawaConfig.Current.SoundEffects) {
            if (soundProjectile != null) {
                var attack = AttackOrigin.Art(soundProjectile)?.Attacks.Find(a => a.ProjectileId == soundProjectile.type);
                attack?.Sounds.TryGetValue(CosmeticAssets.SoundKey(style), out replacement);
            }
            else if (soundPlayer != null) {
                var weapon = CosmeticAssets.Weapon(soundPlayer.GetModPlayer<RiakawaPlayer>().Character,
                    soundPlayer.HeldItem.type);
                if (soundPlayer.HeldItem.UseSound is { } useSound && useSound.SoundPath == style.SoundPath &&
                    useSound.Variants.SequenceEqual(style.Variants)) replacement = weapon?.UseSound;
                else weapon?.Sounds.TryGetValue(CosmeticAssets.SoundKey(style), out replacement);
            }
        }
        var chosen = CosmeticAssets.HasSound(replacement) ? ReplacementSound(style, replacement!) : style;
        return orig(ref chosen, position, callback);
    }

    private static SoundStyle ReplacementSound(SoundStyle source, string path) => new("Riakawa/" + path, source.Type) {
        Identifier = source.Identifier == null ? null : "Riakawa/" + source.Identifier,
        Volume = source.Volume * 0.65f,
        Pitch = source.Pitch + (source.SoundPath is "Terraria/Sounds/Item_26" or
            "Terraria/Sounds/Item_35" or "Terraria/Sounds/Item_47" ? Main.musicPitch : 0f),
        PitchVariance = source.PitchVariance,
        IsLooped = source.IsLooped,
        PauseBehavior = source.PauseBehavior,
        PlayOnlyIfFocused = source.PlayOnlyIfFocused,
        MaxInstances = source.MaxInstances,
        SoundLimitBehavior = source.SoundLimitBehavior
    };
}
