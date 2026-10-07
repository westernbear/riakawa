using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Riakawa;

public sealed class AttackOrigin : GlobalProjectile
{
    public override bool InstancePerEntity => true;
    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type < ProjectileID.Count;
    public ushort WeaponId;

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        int weapon = source is EntitySource_ItemUse use ? use.Item.type :
            source is EntitySource_Parent { Entity: Projectile parent } && parent.type < ProjectileID.Count
                ? parent.GetGlobalProjectile<AttackOrigin>().WeaponId : 0;
        // Vanilla creates these bodies from a Misc source after counting the
        // staff's invisible counters. The held item may already have changed.
        int counter = source is EntitySource_Misc misc ? (misc.Context, projectile.type) switch {
            ("StormTigerTierSwap", 833 or 834 or 835) => 831,
            ("AbigailTierSwap", 963) => 970,
            _ => 0
        } : 0;
        if (counter != 0 && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            foreach (var candidate in Main.projectile)
                if (candidate.active && candidate.owner == projectile.owner && candidate.type == counter) {
                    weapon = candidate.GetGlobalProjectile<AttackOrigin>().WeaponId;
                    if (weapon != 0) break;
                }
        WeaponId = (ushort)(weapon > 0 && weapon < ItemID.Count ? weapon : 0);
    }

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        => binaryWriter.Write(WeaponId);

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
    {
        ushort id = binaryReader.ReadUInt16();
        WeaponId = id < ItemID.Count ? id : (ushort)0;
    }

    internal static WeaponArt? Art(Projectile projectile)
    {
        if (projectile.type >= ProjectileID.Count || projectile.hostile || projectile.owner < 0 ||
            projectile.owner >= Main.maxPlayers || !Main.player[projectile.owner].active) return null;
        return CosmeticAssets.Weapon(Main.player[projectile.owner].GetModPlayer<RiakawaPlayer>().Character,
            projectile.GetGlobalProjectile<AttackOrigin>().WeaponId);
    }
}
