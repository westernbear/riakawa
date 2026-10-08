using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Riakawa.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
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

    // Doro holds swung and aimed items in its own paw: swings sweep it from raised to lowered,
    // guns, bows and staffs point it where the item aims.
    internal static bool DoroArmActive(in PlayerDrawSet draw) => Active(draw) &&
        draw.drawPlayer.HeldItem.useStyle is ItemUseStyleID.Swing or ItemUseStyleID.Shoot &&
        !draw.drawPlayer.mount.Active && !draw.drawPlayer.wet &&
        draw.drawPlayer.GetModPlayer<RiakawaPlayer>().Character == Character.Doro;
    internal static bool Aims(Player player) => player.HeldItem.useStyle == ItemUseStyleID.Shoot;

    // Doro's own front paw, cut from its idle frame by .tools/doro-arm.py: pivot at the shoulder, resting
    // 35 degrees below horizontal, the grip 6 screen pixels out at the sheet's 0.5 scale.
    private static Asset<Texture2D>? doroArm;
    private static readonly Vector2 ArmPivot = new(3, 5);
    private const float ArmRest = 0.61f, ArmReach = 6f;

    // World positions of the shoulder and of the paw tip; angle is measured as if facing right.
    internal static Vector2 DoroPaw(in PlayerDrawSet draw, out Vector2 shoulder, out float angle)
    {
        var player = draw.drawPlayer;
        shoulder = draw.Position + player.legPosition + new Vector2(player.width/2f + player.direction*8.5f,
            player.gravDir == 1 ? player.height-17.5f : 17.5f);
        float swing = 1f-(float)player.itemAnimation/System.Math.Max(1, player.itemAnimationMax);
        angle = Aims(player) ? player.itemRotation*player.direction // vanilla aim, mirrored when facing left
            : MathHelper.ToRadians(-120+180*swing); // raised above the head, down to the front
        return shoulder+new Vector2(player.direction*MathF.Cos(angle), player.gravDir*MathF.Sin(angle))*ArmReach;
    }

    // Drawn after the held item so the paw closes over the grip.
    private static void DoroArm(ref PlayerDrawSet draw)
    {
        var player = draw.drawPlayer;
        DoroPaw(draw, out var shoulder, out float angle);
        doroArm ??= ModContent.Request<Texture2D>("Riakawa/Assets/Players/doro-arm", AssetRequestMode.ImmediateLoad);
        var texture = doroArm.Value;
        var origin = ArmPivot;
        var effects = SpriteEffects.None;
        if (player.direction == -1) { effects |= SpriteEffects.FlipHorizontally; origin.X = texture.Width-origin.X; }
        if (player.gravDir == -1) { effects |= SpriteEffects.FlipVertically; origin.Y = texture.Height-origin.Y; }
        draw.DrawDataCache.Add(new DrawData(texture, shoulder-Main.screenPosition, null, draw.colorArmorBody,
            player.direction*player.gravDir*(angle-ArmRest), origin, .5f, effects, 0));
    }

    internal static void Arm(ref PlayerDrawSet draw)
    {
        if (!Active(draw) || DoroArmActive(draw)) return;
        var player=draw.drawPlayer;
        bool doro=player.GetModPlayer<RiakawaPlayer>().Character==Character.Doro;
        var hand=draw.ItemLocation-Main.screenPosition;
        // Doro's near paw is cut from the body sprite while it swings, so it starts at that paw's shoulder.
        int side=doro?8:10,height=doro?17:13;
        var shoulder=draw.Position+player.legPosition-Main.screenPosition+new Vector2(player.width/2f+player.direction*side,
            player.gravDir==1?player.height-height:height);
        var delta=hand-shoulder;
        if (delta.LengthSquared()>72*72) return; // Native use style has not supplied a grip yet.
        Colors(draw,out var outline,out var fill);
        Segment(ref draw,shoulder,delta,doro?6:5,outline);
        Segment(ref draw,shoulder,delta,doro?4:3,fill);
    }

    private static void Segment(ref PlayerDrawSet draw,Vector2 start,Vector2 delta,float width,Color color)
        => draw.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value,start,new Rectangle(0,0,1,1),color,delta.ToRotation(),
            new Vector2(0,.5f),new Vector2(delta.Length(),width),SpriteEffects.None,0));

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        if (DoroArmActive(drawInfo)) { DoroArm(ref drawInfo); return; }
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
        var character=draw.drawPlayer.GetModPlayer<RiakawaPlayer>().Character;
        outline=(character==Character.Doro?new Color(10,4,7):new Color(64,39,31)).MultiplyRGBA(draw.colorArmorBody);
        fill=(character==Character.Usagi?new Color(255,240,191):character==Character.Doro?Color.White:
            new Color(250,247,247)).MultiplyRGBA(draw.colorArmorBody);
    }

    private static void PixelBox(ref PlayerDrawSet draw,Vector2 position,Vector2 size,Color color)
        => draw.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value,position.Floor(),new Rectangle(0,0,1,1),color,0,
            new Vector2(.5f),size,SpriteEffects.None,0));
}
