using Microsoft.Xna.Framework;
using Riakawa.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Riakawa;

public sealed class RiakawaPlayer : ModPlayer
{
    public Character Character;
    public EmoteClock Emote = new();
    internal int HurtTicks;
    internal int DeathTicks;

    public override void Initialize()
    {
        Character = Character.Original;
        Emote = new();
        HurtTicks = 0;
        DeathTicks = 0;
    }

    public override void OnEnterWorld()
    {
        if (Player.whoAmI == Main.myPlayer) ReadConfig();
    }

    private void ReadConfig()
    {
        var value = RiakawaConfig.Current.Character;
        Character = value <= Character.Momonga ? value : Character.Original;
    }

    public override void PreUpdate()
    {
        if (!Main.dedServ && Player.whoAmI == Main.myPlayer) ReadConfig();
    }

    public override void PostUpdate()
    {
        Emote.Tick();
        if (HurtTicks > 0) HurtTicks--;
    }

    public override void UpdateDead() => DeathTicks++;
    public override void OnRespawn() => DeathTicks = 0;

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (Main.gameMenu && !Main.dedServ) ReadConfig();
    }

    public override void CopyClientState(ModPlayer targetCopy) => ((RiakawaPlayer)targetCopy).Character = Character;
    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if (((RiakawaPlayer)clientPlayer).Character != Character)
            SyncPlayer(-1, -1, false);
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (!Main.dedServ && Player.whoAmI == Main.myPlayer) ReadConfig();
        ((Riakawa)Mod).Send(new(MessageKind.State, (byte)Player.whoAmI, Character,
            Main.netMode == NetmodeID.Server ? Emote.Remaining : (ushort)0), toWho, fromWho);
    }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (Riakawa.EmoteKey?.JustPressed != true || Player.dead || Character == Character.Original)
            return;
        if (Main.netMode == NetmodeID.MultiplayerClient) {
            if (Emote.Cooldown == 0 && Emote.TryStart(Character, true))
                ((Riakawa)Mod).Send(new(MessageKind.Emote, (byte)Player.whoAmI, Character, 0));
        }
        else if (Emote.TryStart(Character, true)) PlayEmote();
    }

    internal void PlayEmote()
    {
        if (Main.dedServ || Character == Character.Original) return;
        if (RiakawaConfig.Current.Decorations)
            CombatText.NewText(Player.Hitbox, Color.White,
                Language.GetTextValue($"Mods.Riakawa.Emotes.{Character}"), dramatic: false);
        CosmeticAssets.PlayVoice(Character, "emote", Player.Center);
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        HurtTicks = 18;
        if (!info.SoundDisabled) CosmeticAssets.PlayVoice(Character, "hurt", Player.Center);
    }

    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound,
        ref bool genDust, ref PlayerDeathReason damageSource)
    {
        if (CosmeticAssets.HasVoice(Character, "death")) playSound = false;
        if (CosmeticAssets.TryPlayer(Character, out _, out _)) genDust = false;
        return true;
    }

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        => CosmeticAssets.PlayVoice(Character, "death", Player.Center);

    public override void TransformDrawData(ref PlayerDrawSet drawInfo) => WeaponVisuals.ReplaceHeldLayers(ref drawInfo);

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        if (!CosmeticAssets.TryPlayer(Character, out _, out _)) return;
        // Only vanilla body/vanity layers: wings, mounts, debuff indicators and held attacks remain.
        foreach (var layer in BodyLayers) layer.Hide();
    }

    private static readonly PlayerDrawLayer[] BodyLayers = [
        PlayerDrawLayers.JimsCloak, PlayerDrawLayers.SafemanSun, PlayerDrawLayers.LeinforsHairShampoo,
        PlayerDrawLayers.Backpacks, PlayerDrawLayers.Tails, PlayerDrawLayers.HairBack, PlayerDrawLayers.BackAcc,
        PlayerDrawLayers.HeadBack, PlayerDrawLayers.BalloonAcc, PlayerDrawLayers.Skin, PlayerDrawLayers.Leggings,
        PlayerDrawLayers.Shoes, PlayerDrawLayers.Robe, PlayerDrawLayers.SkinLongCoat, PlayerDrawLayers.ArmorLongCoat,
        PlayerDrawLayers.Torso, PlayerDrawLayers.OffhandAcc, PlayerDrawLayers.WaistAcc, PlayerDrawLayers.NeckAcc,
        PlayerDrawLayers.Head, PlayerDrawLayers.FinchNest, PlayerDrawLayers.Magiluminescence, PlayerDrawLayers.FaceAcc,
        PlayerDrawLayers.FrontAccBack, PlayerDrawLayers.Shield, PlayerDrawLayers.ArmOverItem,
        PlayerDrawLayers.HandOnAcc, PlayerDrawLayers.FrontAccFront, PlayerDrawLayers.EyebrellaCloud
    ];
}
