using UnityEngine;

namespace VampFrost
{
    public enum AmbientType { Graveyard, Village, Forest, Ruins, City, Desert }

    public class MapDef
    {
        public int id;
        public string name;
        public Color bg, groundA, groundB, decoA, decoB;
        public int[] mobs;          // indices into EnemyDefs.Mobs
        public int bossId;          // index into EnemyDefs.Bosses
        public AmbientType ambient;
        public AudioReverbPreset reverb;
        public DecoShape[] shapes;
    }

    public static class MapDefs
    {
        public static readonly MapDef[] All =
        {
            new MapDef { id=0, name="Graveyard",
                bg=new Color(.05f,.07f,.09f), groundA=new Color(.16f,.20f,.17f), groundB=new Color(.13f,.16f,.14f),
                decoA=new Color(.42f,.46f,.50f), decoB=new Color(.28f,.30f,.34f),
                mobs=new[]{0,1,2}, bossId=0, ambient=AmbientType.Graveyard,
                reverb=AudioReverbPreset.Stoneroom,
                shapes=new[]{DecoShape.Tombstone, DecoShape.Cross, DecoShape.DeadTree, DecoShape.Fence, DecoShape.Rock} },

            new MapDef { id=1, name="Plague Village",
                bg=new Color(.07f,.08f,.06f), groundA=new Color(.24f,.20f,.14f), groundB=new Color(.19f,.16f,.11f),
                decoA=new Color(.38f,.26f,.14f), decoB=new Color(.22f,.15f,.09f),
                mobs=new[]{3,4,5}, bossId=1, ambient=AmbientType.Village,
                reverb=AudioReverbPreset.Off,
                shapes=new[]{DecoShape.Fence, DecoShape.Barrel, DecoShape.DeadTree, DecoShape.Bush} },

            new MapDef { id=2, name="Cursed Forest",
                bg=new Color(.03f,.06f,.05f), groundA=new Color(.10f,.16f,.10f), groundB=new Color(.08f,.13f,.08f),
                decoA=new Color(.15f,.10f,.08f), decoB=new Color(.10f,.18f,.10f),
                mobs=new[]{6,7,8}, bossId=2, ambient=AmbientType.Forest,
                reverb=AudioReverbPreset.Forest,
                shapes=new[]{DecoShape.DeadTree, DecoShape.Bush, DecoShape.Rock, DecoShape.Rock} },

            new MapDef { id=3, name="Gothic Cathedral",
                bg=new Color(.06f,.05f,.09f), groundA=new Color(.22f,.20f,.26f), groundB=new Color(.17f,.15f,.21f),
                decoA=new Color(.40f,.36f,.46f), decoB=new Color(.26f,.22f,.32f),
                mobs=new[]{9,10,11}, bossId=3, ambient=AmbientType.Ruins,
                reverb=AudioReverbPreset.Cave,
                shapes=new[]{DecoShape.Pillar, DecoShape.Pillar, DecoShape.Cross, DecoShape.Rock} },

            new MapDef { id=4, name="NYC Streets",
                bg=new Color(.05f,.05f,.07f), groundA=new Color(.20f,.20f,.23f), groundB=new Color(.16f,.16f,.19f),
                decoA=new Color(.30f,.30f,.34f), decoB=new Color(.85f,.60f,.10f),
                mobs=new[]{12,13,14}, bossId=4, ambient=AmbientType.City,
                reverb=AudioReverbPreset.City,
                shapes=new[]{DecoShape.Car, DecoShape.Barrel, DecoShape.Fence, DecoShape.Rock} },

            new MapDef { id=5, name="Arab Desert",
                bg=new Color(.10f,.08f,.05f), groundA=new Color(.55f,.44f,.27f), groundB=new Color(.48f,.38f,.23f),
                decoA=new Color(.66f,.56f,.36f), decoB=new Color(.20f,.35f,.16f),
                mobs=new[]{15,16,17}, bossId=6, ambient=AmbientType.Desert,
                reverb=AudioReverbPreset.Plain,
                shapes=new[]{DecoShape.Cactus, DecoShape.Pillar, DecoShape.Rock, DecoShape.Rock} },
        };
    }
}
