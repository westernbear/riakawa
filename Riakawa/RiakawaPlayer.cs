using Riakawa.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Riakawa;

public sealed class RiakawaPlayer : ModPlayer
{
    public Character Character;
    internal int HurtTicks;
    internal int DeathTicks;

    public override void Initialize()
    {
        Character = Character.Original;
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
        Character = value <= Character.Doro ? value : Character.Original;
    }

    public override void PreUpdate()
    {
        if (!Main.dedServ && Player.whoAmI == Main.myPlayer) ReadConfig();
    }

    public override void PostUpdate()
    {
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
        ((Riakawa)Mod).Send(new((byte)Player.whoAmI, Character), toWho, fromWho);
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        HurtTicks = 18;
    }

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
