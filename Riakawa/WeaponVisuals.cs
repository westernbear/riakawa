using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Riakawa;

public sealed class WeaponVisuals : GlobalItem
{
    private static bool TryArt(Item item, Player player, out Texture2D texture)
    {
        texture = null!;
        if (!RiakawaConfig.Current.ReplaceWeapons) return false;
        var art = CosmeticAssets.Weapon(player.GetModPlayer<RiakawaPlayer>().Character, item.type);
        if (art == null || !CosmeticAssets.TryTexture(art.Texture, out var asset)) return false;
        Main.instance.LoadItem(item.type);
        if (asset.Value.Bounds != TextureAssets.Item[item.type].Value.Bounds) return false;
        texture = asset.Value;
        return true;
    }

    public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position,
        Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (!TryArt(item, Main.LocalPlayer, out var texture)) return true;
        spriteBatch.Draw(texture, position, frame, drawColor, 0, origin, scale, SpriteEffects.None, 0);
        return false;
    }

    public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
        ref float rotation, ref float scale, int whoAmI)
    {
        if (!TryArt(item, Main.LocalPlayer, out var texture)) return true;
        Main.GetItemDrawFrame(item.type, out _, out var frame);
        Vector2 origin = frame.Size() / 2;
        spriteBatch.Draw(texture, item.Bottom - Main.screenPosition - new Vector2(0, origin.Y),
            frame, item.GetAlpha(lightColor), rotation, origin, scale, SpriteEffects.None, 0);
        return false;
    }

    // Some native held items and extra glows bypass the item draw hook.
    // Replace completed draw data without moving the native weapon or effect.
    internal static void ReplaceHeldLayers(ref PlayerDrawSet drawInfo)
    {
        if (!RiakawaConfig.Current.ReplaceWeapons) return;
        var art = CosmeticAssets.Weapon(drawInfo.drawPlayer.GetModPlayer<RiakawaPlayer>().Character,drawInfo.heldItem.type);
        if (art == null || art.HeldBindings.Count == 0) return;
        foreach (var binding in art.HeldBindings) {
            var slots = binding.Slot switch { "Item" => TextureAssets.Item, "Extra" => TextureAssets.Extra,
                "GlowMask" => TextureAssets.GlowMask, "ItemFlame" => TextureAssets.ItemFlame, _ => null };
            if (slots == null || binding.Id < 0 || binding.Id >= slots.Length ||
                !CosmeticAssets.TryTexture(binding.Texture,out var replacement)) continue;
            var original = slots[binding.Id].Value;
            if (original.Bounds != replacement.Value.Bounds) continue;
            for (int i=0;i<drawInfo.DrawDataCache.Count;i++) {
                var data=drawInfo.DrawDataCache[i];
                if (ReferenceEquals(data.texture,original)) {
                    data.texture=replacement.Value; drawInfo.DrawDataCache[i]=data;
                }
            }
        }
    }

    public override void PreModifyItemDraw(Item item, ref PlayerDrawSet drawInfo, ref DrawData drawData,
        ref DrawData? coloredDrawData, ref DrawData? glowMaskDrawData)
    {
        if (!TryArt(item, drawInfo.drawPlayer, out var texture)) return;
        drawData.texture = texture;
        if (coloredDrawData is { } color) coloredDrawData = color with { texture = texture };
    }
}
