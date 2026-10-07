using Microsoft.Xna.Framework;
using Riakawa.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Riakawa;

public sealed class CharacterLayer : PlayerDrawLayer
{
    public override bool IsHeadLayer => true;
    // An independent layer must not be parented to one of the hidden body layers.
    public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.NeckAcc, PlayerDrawLayers.Head);

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => !drawInfo.drawPlayer.invis &&
        CosmeticAssets.TryPlayer(drawInfo.drawPlayer.GetModPlayer<RiakawaPlayer>().Character, out _, out _);

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        var player = drawInfo.drawPlayer;
        var state = player.GetModPlayer<RiakawaPlayer>();
        if (!CosmeticAssets.TryPlayer(state.Character, out var art, out var texture)) return;
        string groundMotion = state.Character == Character.Doro &&
            SkinMotion.FastRun(player.velocity.X, player.maxRunSpeed, player.accRunSpeed,
                player.velocity.X < 0 ? player.controlLeft : player.controlRight,
                player.velocity.Y == 0, player.mount.Active) ? "run" : "walk";
        string animation = player.dead ? "death" : state.HurtTicks > 0 ? "hurt" :
            player.itemAnimation > 0 ? "attack" : player.mount.Active ? "mount" :
            player.wet ? "swim" : player.velocity.Y != 0 ? "jump" :
            System.Math.Abs(player.velocity.X) > 0.1f ? groundMotion : "idle";
        // The native grip supplies the attacking arm; keep the body's movement.
        if (animation == "attack" && (CharacterHands.Active(drawInfo) || groundMotion == "run"))
            animation = player.mount.Active ? "mount" : player.wet ? "swim" :
                player.velocity.Y != 0 ? "jump" : System.Math.Abs(player.velocity.X) > 0.1f ? groundMotion : "idle";
        if (!art.Animations.TryGetValue(animation, out var sequence) &&
            !art.Animations.TryGetValue("idle", out sequence)) return;
        int frame = (int)(Main.GameUpdateCount / (uint)System.Math.Max(1, sequence.Ticks)
            % (uint)System.Math.Max(1, sequence.Frames));
        if (animation == "attack" && player.itemAnimationMax > 0)
            frame = System.Math.Clamp((player.itemAnimationMax - player.itemAnimation) * sequence.Frames /
                player.itemAnimationMax, 0, sequence.Frames - 1);
        if (animation == "death") frame = System.Math.Min(state.DeathTicks / sequence.Ticks, sequence.Frames - 1);
        if (animation == "jump") frame = player.velocity.Y * player.gravDir < 0 ? 0 : 1;
        CharacterHands.Arm(ref drawInfo);
        var source = new Rectangle(frame * art.FrameWidth, sequence.Row * art.FrameHeight, art.FrameWidth, art.FrameHeight);
        var position = drawInfo.Position + new Vector2(player.width / 2f,
            player.gravDir == 1 ? player.height : 0) + (player.dead ? Vector2.Zero : player.legPosition) - Main.screenPosition;
        var origin = new Vector2(art.Feet[0], player.gravDir == 1 ? art.Feet[1] : art.FrameHeight - art.Feet[1]);
        var effects = player.direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        if (player.gravDir == -1) effects |= SpriteEffects.FlipVertically;
        if (player.direction == -1) origin.X = art.FrameWidth - origin.X;
        if (drawInfo.headOnlyRender) {
            source = new Rectangle(art.Head[0], art.Head[1], art.Head[2], art.Head[3]);
            position = drawInfo.Position - Main.screenPosition + new Vector2(player.width / 2f, player.height / 2f);
            origin = source.Size() / 2;
        }
        drawInfo.DrawDataCache.Add(new DrawData(texture.Value, position, source,
            drawInfo.headOnlyRender ? drawInfo.colorArmorHead : drawInfo.colorArmorBody,
            0f, origin, 40f / art.FrameWidth, effects, 0));
    }
}
