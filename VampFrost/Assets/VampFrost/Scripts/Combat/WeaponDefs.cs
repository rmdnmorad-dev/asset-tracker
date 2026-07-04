using UnityEngine;

namespace VampFrost
{
    public enum ProjBehavior { Straight, Boomerang, Orbit, OrbitOut, Blast, Pool, Ring, ArcSwing, Fang }
    public enum AimMode { Nearest, MoveDir, RandomEnemy, RandomDir, Self }

    public class WeaponDef
    {
        public string name, desc;
        public float cd, dmg, speed, life, area = 1f, scale = 1f;
        public int count = 1, pierce;
        public float frost, steal;
        public float pitch = 1f;
        public ProjBehavior beh;
        public AimMode aim;
        public bool grow;       // gains +count at lvl 3 / 6
        public bool usesAmount; // benefits from the Amount passive
        public Color col;
        public ProjShape shape;
        public const int MaxLevel = 8;
    }

    public static class WeaponDefs
    {
        static readonly Color Ice = new Color(.55f, .88f, 1f);
        static readonly Color Blood = new Color(.75f, .10f, .16f);
        static readonly Color IceBlood = new Color(.72f, .35f, .55f);
        static readonly Color Bone = new Color(.90f, .87f, .74f);
        static readonly Color Shadow = new Color(.22f, .16f, .30f);

        public static readonly WeaponDef[] All =
        {
            /* 0 */ new WeaponDef{ name="Icicle Spike", desc="Piercing icicle toward the nearest foe",
                cd=1.0f, dmg=10, speed=12, life=1.5f, pierce=2, frost=.30f, pitch=1.25f,
                beh=ProjBehavior.Straight, aim=AimMode.Nearest, grow=true, usesAmount=true,
                col=Ice, shape=ProjShape.Shard },

            /* 1 */ new WeaponDef{ name="Frozen-Blood Scythe", desc="Sweeping arc around you",
                cd=1.6f, dmg=18, speed=520, life=.30f, pierce=99, frost=.20f, steal=.05f, pitch=.85f,
                beh=ProjBehavior.ArcSwing, aim=AimMode.Self, grow=true, scale=1.5f,
                col=IceBlood, shape=ProjShape.Blade },

            /* 2 */ new WeaponDef{ name="Frozen-Blood Spear", desc="Long pierce along your movement",
                cd=1.4f, dmg=14, speed=16, life=.9f, pierce=99, frost=.25f, pitch=1.05f,
                beh=ProjBehavior.Straight, aim=AimMode.MoveDir, scale=1.5f,
                col=IceBlood, shape=ProjShape.Spear },

            /* 3 */ new WeaponDef{ name="Bone Lance", desc="Heavy lance skewers a random enemy",
                cd=2.4f, dmg=40, speed=10, life=1.6f, pierce=99, pitch=.7f,
                beh=ProjBehavior.Straight, aim=AimMode.RandomEnemy, scale=1.9f,
                col=Bone, shape=ProjShape.Spear },

            /* 4 */ new WeaponDef{ name="Shard Bolts", desc="Fan of frost shards ahead",
                cd=1.2f, dmg=7, speed=11, life=1.2f, count=3, pierce=0, frost=.35f, pitch=1.4f,
                beh=ProjBehavior.Straight, aim=AimMode.MoveDir, grow=true, usesAmount=true,
                col=Ice, shape=ProjShape.Shard },

            /* 5 */ new WeaponDef{ name="Spinning Disc", desc="Boomerangs through the horde",
                cd=1.8f, dmg=12, speed=10, life=3f, pierce=99, frost=.15f, pitch=1.1f,
                beh=ProjBehavior.Boomerang, aim=AimMode.Nearest, grow=true, usesAmount=true, scale=1.2f,
                col=IceBlood, shape=ProjShape.Disc },

            /* 6 */ new WeaponDef{ name="Frozen-Blood Orb", desc="Orbs orbit and grind",
                cd=0, dmg=10, speed=140, life=0, pierce=99, frost=.30f, steal=.03f, pitch=1f,
                beh=ProjBehavior.Orbit, aim=AimMode.Self, grow=true, usesAmount=true,
                col=IceBlood, shape=ProjShape.Orb },

            /* 7 */ new WeaponDef{ name="Explosion Burst", desc="Frost detonation on a random enemy",
                cd=2.6f, dmg=30, speed=0, life=.25f, pierce=99, frost=.25f, area=1.6f, pitch=.75f,
                beh=ProjBehavior.Blast, aim=AimMode.RandomEnemy,
                col=Ice, shape=ProjShape.RingO },

            /* 8 */ new WeaponDef{ name="Blood Pool", desc="Draining pool under the nearest foe",
                cd=3.0f, dmg=7, speed=0, life=3f, pierce=99, steal=.15f, area=1.4f, pitch=.6f,
                beh=ProjBehavior.Pool, aim=AimMode.Nearest,
                col=Blood, shape=ProjShape.Pool },

            /* 9 */ new WeaponDef{ name="Shadow Scythe", desc="Scythes spiral outward",
                cd=1.5f, dmg=13, speed=260, life=2.0f, count=2, pierce=99, pitch=.9f,
                beh=ProjBehavior.OrbitOut, aim=AimMode.Self, grow=true, usesAmount=true, scale=1.2f,
                col=Shadow, shape=ProjShape.Blade },

            /* 10 */ new WeaponDef{ name="Bite Dash", desc="Your dash tears with fangs (dash CD -10%/lv)",
                cd=0, dmg=25, speed=0, life=.2f, pierce=99, steal=.20f, area=1.3f, pitch=.95f,
                beh=ProjBehavior.Fang, aim=AimMode.Self,
                col=Blood, shape=ProjShape.Fang },

            /* 11 */ new WeaponDef{ name="Blood Bolt", desc="Rapid bolts, drink on hit",
                cd=.55f, dmg=6, speed=14, life=1.1f, pierce=0, steal=.08f, pitch=1.3f,
                beh=ProjBehavior.Straight, aim=AimMode.Nearest, grow=true, usesAmount=true,
                col=Blood, shape=ProjShape.Bolt },

            /* 12 */ new WeaponDef{ name="Crescent Blade", desc="Huge slow crescent cuts a lane",
                cd=2.0f, dmg=26, speed=7, life=1.3f, pierce=99, pitch=.8f, scale=2.3f,
                beh=ProjBehavior.Straight, aim=AimMode.MoveDir, grow=true, usesAmount=true,
                col=IceBlood, shape=ProjShape.Blade },

            /* 13 */ new WeaponDef{ name="Throwing Stars", desc="Stars scatter in all directions",
                cd=1.1f, dmg=8, speed=13, life=1.4f, count=4, pierce=1, pitch=1.45f,
                beh=ProjBehavior.Straight, aim=AimMode.RandomDir, grow=true, usesAmount=true,
                col=Blood, shape=ProjShape.Star },

            /* 14 */ new WeaponDef{ name="Nova Ring", desc="Frost ring erupts outward",
                cd=3.2f, dmg=22, speed=9, life=.75f, pierce=99, frost=.40f, area=1f, pitch=.65f,
                beh=ProjBehavior.Ring, aim=AimMode.Self,
                col=Ice, shape=ProjShape.RingO },
        };
    }
}
