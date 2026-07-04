using UnityEngine;

namespace VampFrost
{
    public class MobDef
    {
        public string key, name;
        public Color color;
        public float hp, dmg, speed, radius, size, xp;
        public bool ranged;
        public float range, fireCd, projSpeed;
    }

    public struct BossPhase
    {
        public float summonEvery, radialEvery, burstEvery, chargeEvery, teleportEvery, ringEvery, aoeEvery;
        public int radialCount, burstCount, summonId;
        public float speedMul;
    }

    public class BossDef
    {
        public string key, name;
        public Color color;
        public float hp, dmg, speed, size;
        public BossPhase[] phases; // 3 phases
    }

    public static class EnemyDefs
    {
        // Indices referenced by MapDefs.mobs
        public static readonly MobDef[] Mobs =
        {
            // 0-2 GRAVEYARD
            new MobDef{ key="GraveKnight",   name="Grave Knight",    color=new Color(.45f,.50f,.58f), hp=16, dmg=7, speed=1.9f, radius=.35f, size=1.05f, xp=1.2f },
            new MobDef{ key="PhantomWraith", name="Phantom Wraith",  color=new Color(.35f,.85f,.90f), hp=8,  dmg=5, speed=3.1f, radius=.30f, size=.95f,  xp=1f },
            new MobDef{ key="CryptGuardian", name="Crypt Guardian",  color=new Color(.78f,.75f,.62f), hp=34, dmg=10, speed=1.2f, radius=.42f, size=1.25f, xp=2f },
            // 3-5 VILLAGE
            new MobDef{ key="PlagueBearer",  name="Plague Bearer",   color=new Color(.35f,.62f,.20f), hp=14, dmg=6, speed=1.6f, radius=.33f, size=1f,    xp=1.1f },
            new MobDef{ key="InfectedBrute", name="Infected Brute",  color=new Color(.70f,.30f,.24f), hp=38, dmg=12, speed=1.4f, radius=.45f, size=1.3f,  xp=2.2f },
            new MobDef{ key="PlagueCultist", name="Plague Cultist",  color=new Color(.30f,.16f,.36f), hp=11, dmg=8, speed=1.7f, radius=.30f, size=1f,    xp=1.4f,
                        ranged=true, range=6.5f, fireCd=2.6f, projSpeed=5.5f },
            // 6-8 FOREST
            new MobDef{ key="FeralDirewolf", name="Feral Direwolf",  color=new Color(.25f,.27f,.32f), hp=10, dmg=6, speed=3.4f, radius=.32f, size=1f,    xp=1f },
            new MobDef{ key="CursedBear",    name="Cursed Bear",     color=new Color(.36f,.22f,.16f), hp=46, dmg=14, speed=1.3f, radius=.5f,  size=1.45f, xp=2.6f },
            new MobDef{ key="WildHuntsman",  name="Wild Huntsman",   color=new Color(.60f,.48f,.30f), hp=12, dmg=9, speed=1.8f, radius=.32f, size=1.05f, xp=1.5f,
                        ranged=true, range=7.5f, fireCd=2.4f, projSpeed=8f },
            // 9-11 GOTHIC RUINS
            new MobDef{ key="BloodSorcerer", name="Blood Sorcerer",  color=new Color(.62f,.10f,.16f), hp=13, dmg=10, speed=1.6f, radius=.31f, size=1.05f, xp=1.6f,
                        ranged=true, range=7f, fireCd=2.2f, projSpeed=6f },
            new MobDef{ key="Gargoyle",      name="Gargoyle",        color=new Color(.52f,.52f,.56f), hp=20, dmg=8, speed=2.6f, radius=.36f, size=1.1f,  xp=1.6f },
            new MobDef{ key="CorruptedWarrior", name="Corrupted Warrior", color=new Color(.55f,.14f,.14f), hp=30, dmg=12, speed=1.9f, radius=.40f, size=1.2f, xp=2.2f },
            // 12-14 NYC
            new MobDef{ key="UrbanEnforcer", name="Urban Enforcer",  color=new Color(.16f,.20f,.34f), hp=15, dmg=8, speed=2.0f, radius=.33f, size=1.05f, xp=1.5f,
                        ranged=true, range=7f, fireCd=1.9f, projSpeed=9f },
            new MobDef{ key="RiotTank",      name="Riot Tank",       color=new Color(.40f,.44f,.50f), hp=55, dmg=14, speed=1.1f, radius=.5f,  size=1.4f,  xp=3f },
            new MobDef{ key="SniperAssassin",name="Sniper Assassin", color=new Color(.30f,.36f,.22f), hp=10, dmg=16, speed=1.7f, radius=.30f, size=1f,   xp=1.8f,
                        ranged=true, range=10f, fireCd=3.4f, projSpeed=13f },
            // 15-17 DESERT
            new MobDef{ key="SandWraith",    name="Sand Wraith",     color=new Color(.35f,.32f,.42f), hp=11, dmg=6, speed=3.2f, radius=.31f, size=1f,   xp=1.1f },
            new MobDef{ key="CursedNomad",   name="Cursed Nomad",    color=new Color(.72f,.55f,.32f), hp=16, dmg=9, speed=1.9f, radius=.33f, size=1.05f, xp=1.5f,
                        ranged=true, range=6.5f, fireCd=2.4f, projSpeed=7f },
            new MobDef{ key="ShadowSerpent", name="Shadow Serpent",  color=new Color(.10f,.30f,.32f), hp=18, dmg=10, speed=2.8f, radius=.36f, size=1.15f, xp=1.7f },
        };

        static BossPhase P(float summon = 0, float radialE = 0, int radialC = 0, float burstE = 0, int burstC = 0,
                           float charge = 0, float tele = 0, float ring = 0, float aoe = 0, float spd = 1, int summonId = 0)
            => new BossPhase { summonEvery = summon, radialEvery = radialE, radialCount = radialC,
                               burstEvery = burstE, burstCount = burstC, chargeEvery = charge,
                               teleportEvery = tele, ringEvery = ring, aoeEvery = aoe,
                               speedMul = spd, summonId = summonId };

        public static readonly BossDef[] Bosses =
        {
            new BossDef{ key="CrimsonInquisitor", name="The Crimson Inquisitor", color=new Color(.78f,.14f,.14f),
                hp=5200, dmg=20, speed=1.7f, size=2.4f, phases=new[]{
                    P(charge:4.5f, spd:1f),
                    P(charge:3.6f, radialE:3.5f, radialC:8, spd:1.15f),
                    P(charge:2.8f, radialE:2.4f, radialC:12, aoe:4f, spd:1.3f) } },

            new BossDef{ key="PlagueNecromancer", name="Plague Necromancer", color=new Color(.30f,.60f,.18f),
                hp=4600, dmg=16, speed=1.4f, size=2.2f, phases=new[]{
                    P(summon:3f, summonId:3, spd:1f),
                    P(summon:2.2f, summonId:3, aoe:4.5f, spd:1.05f),
                    P(summon:1.6f, summonId:4, radialE:3f, radialC:10, spd:1.15f) } },

            new BossDef{ key="AlphaWerewolf", name="Alpha Werewolf", color=new Color(.22f,.22f,.28f),
                hp=5000, dmg=22, speed=2.6f, size=2.3f, phases=new[]{
                    P(charge:3f, spd:1f),
                    P(charge:2.3f, summon:4f, summonId:6, spd:1.2f),
                    P(charge:1.6f, summon:3f, summonId:6, spd:1.5f) } },

            new BossDef{ key="MirrorVampire", name="Mirror Vampire", color=new Color(.62f,.66f,.78f),
                hp=4400, dmg=18, speed=1.9f, size=2.1f, phases=new[]{
                    P(tele:4f, burstE:2.6f, burstC:3, spd:1f),
                    P(tele:3f, burstE:2f, burstC:5, radialE:4f, radialC:8, spd:1.1f),
                    P(tele:2f, burstE:1.4f, burstC:6, radialE:2.6f, radialC:12, spd:1.2f) } },

            new BossDef{ key="CityTyrant", name="City Tyrant", color=new Color(.34f,.40f,.30f),
                hp=6200, dmg=18, speed=1.3f, size=2.5f, phases=new[]{
                    P(burstE:1.8f, burstC:4, spd:1f),
                    P(burstE:1.4f, burstC:6, aoe:4f, spd:1.05f),
                    P(burstE:1f, burstC:8, radialE:2.2f, radialC:14, aoe:3f, spd:1.15f) } },

            new BossDef{ key="HighCommandoManiac", name="High Commando Maniac", color=new Color(.55f,.42f,.28f),
                hp=1600, dmg=14, speed=2.1f, size=1.7f, phases=new[]{  // NYC wave-15 mini boss
                    P(burstE:1.6f, burstC:3, spd:1f),
                    P(burstE:1.2f, burstC:4, aoe:5f, spd:1.1f),
                    P(burstE:.9f, burstC:5, charge:3f, spd:1.25f) } },

            new BossDef{ key="DesertDjinn", name="Desert Djinn", color=new Color(.32f,.20f,.45f),
                hp=4800, dmg=17, speed=1.8f, size=2.3f, phases=new[]{
                    P(tele:4f, ring:3.5f, spd:1f),
                    P(tele:3f, ring:2.8f, radialE:3.5f, radialC:8, spd:1.1f),
                    P(tele:2.2f, ring:2f, radialE:2.4f, radialC:12, spd:1.2f) } },
        };
    }
}
