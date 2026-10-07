using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Riakawa.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Riakawa;

// Draw-only paws connect the round body to the native grip. Weapon positions,
// projectile origins and collision geometry remain the game's own values.
public sealed class CharacterHands : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);
    internal static bool Active(in PlayerDrawSet draw) => !draw.headOnlyRender && !draw.drawPlayer.invis &&
        !draw.drawPlayer.dead && !draw.drawPlayer.frozen && draw.drawPlayer.itemAnimation > 0 &&
        CosmeticAssets.TryPlayer(draw.drawPlayer.GetModPlayer<RiakawaPlayer>().Character,out _,out _);
    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => Active(drawInfo);

    internal static void Arm(ref PlayerDrawSet draw)
    {
        if (!Active(draw)) return;
        var player=draw.drawPlayer;
        var hand=draw.ItemLocation-Main.screenPosition;
        var shoulder=draw.Position+player.legPosition-Main.screenPosition+new Vector2(player.width/2f+player.direction*10,
            player.gravDir==1?player.height-13:13);
        var delta=hand-shoulder;
        if (delta.LengthSquared()>72*72) return; // Native use style has not supplied a grip yet.
        Colors(draw,out var outline,out var fill);
        Segment(ref draw,shoulder,delta,5,outline);
        Segment(ref draw,shoulder,delta,3,fill);
    }

    private static void Segment(ref PlayerDrawSet draw,Vector2 start,Vector2 delta,float width,Color color)
        => draw.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value,start,new Rectangle(0,0,1,1),color,delta.ToRotation(),
            new Vector2(0,.5f),new Vector2(delta.Length(),width),SpriteEffects.None,0));

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        Colors(drawInfo,out var outline,out var fill);
        var hand=drawInfo.ItemLocation-Main.screenPosition;
        var relative=drawInfo.ItemLocation-drawInfo.Position;
        if (relative.LengthSquared()>100*100) return;
        PixelBox(ref drawInfo,hand,new Vector2(5,3),outline);
        PixelBox(ref drawInfo,hand,new Vector2(3,5),outline);
        PixelBox(ref drawInfo,hand,new Vector2(3,3),fill);
    }

    private static void Colors(in PlayerDrawSet draw,out Color outline,out Color fill)
    {
        outline=new Color(64,39,31).MultiplyRGBA(draw.colorArmorBody);
        fill=(draw.drawPlayer.GetModPlayer<RiakawaPlayer>().Character==Character.Usagi?
            new Color(255,240,191):new Color(250,247,247)).MultiplyRGBA(draw.colorArmorBody);
    }

    private static void PixelBox(ref PlayerDrawSet draw,Vector2 position,Vector2 size,Color color)
        => draw.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value,position.Floor(),new Rectangle(0,0,1,1),color,0,
            new Vector2(.5f),size,SpriteEffects.None,0));
}
