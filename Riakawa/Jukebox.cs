using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Riakawa;

// A music box for the original "Siren Island" track (ACE-Step 1.5, text prompt only). It starts playing when
// placed; right-click toggles it like vanilla music boxes, and it also plays from an accessory slot.
public sealed class Jukebox : ModItem
{
    public override string Texture => "Riakawa/Assets/Jukebox/Item";

    public override void SetStaticDefaults()
    {
        ItemID.Sets.CanGetPrefixes[Type] = false;
        ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.MusicBox;
        MusicLoader.AddMusicBox(Mod, MusicLoader.GetMusicSlot(Mod, "Assets/Music/SirenIsland"), Type,
            ModContent.TileType<JukeboxTile>());
    }

    public override void SetDefaults() => Item.DefaultToMusicBox(ModContent.TileType<JukeboxTile>(), 0);

    public override void AddRecipes() => CreateRecipe()
        .AddRecipeGroup(RecipeGroupID.Wood, 4)
        .AddTile(TileID.WorkBenches)
        .Register();
}

public sealed class JukeboxTile : ModTile
{
    public override string Texture => "Riakawa/Assets/Jukebox/Tile";

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileObsidianKill[Type] = true;
        TileID.Sets.HasOutlines[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleLineSkip = 2;
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(232, 170, 160), CreateMapEntryName());
    }

    // Frames 36 and up are "on" for music boxes; start there so the song plays as soon as it is placed.
    public override void PlaceInWorld(int i, int j, Item item)
    {
        var tile = Main.tile[i, j];
        int left = i - tile.TileFrameX % 36 / 18, top = j - tile.TileFrameY % 36 / 18;
        for (int x = left; x < left + 2; x++)
            for (int y = top; y < top + 2; y++)
                Main.tile[x, y].TileFrameX += 36;
        if (Main.netMode == NetmodeID.MultiplayerClient) NetMessage.SendTileSquare(-1, left, top, 2, 2);
    }

    public override void MouseOver(int i, int j)
    {
        var player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType<Jukebox>();
    }

    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

    // Vanilla music boxes float notes while playing.
    public override void EmitParticles(int i, int j, Tile tile, short tileFrameX, short tileFrameY, Color tileLight, bool visible)
    {
        if (!visible || tileFrameX != 36 || tileFrameY % 36 != 0 || (int)Main.timeForVisualEffects % 7 != 0 || !Main.rand.NextBool(3))
            return;
        int note = Main.rand.Next(570, 573); // vanilla music note gores
        var velocity = new Vector2(Main.WindForVisuals * 2f * Main.rand.NextFloat(.5f, 1.5f), -.5f * Main.rand.NextFloat(.5f, 1.5f));
        Gore.NewGore(new EntitySource_TileUpdate(i, j), new Vector2(i * 16 + 8 - (note - 570) * 4, j * 16 - 8),
            velocity, note, .8f);
    }
}
