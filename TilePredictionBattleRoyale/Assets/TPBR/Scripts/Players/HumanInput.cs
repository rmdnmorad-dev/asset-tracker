using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    /// Your side of the game. Prep = walk around and lie with your body language.
    /// Decision = click one tile in your own zone (where you hide) and one tile in
    /// somebody else's (where you strike).
    public class HumanInput
    {
        public PlayerState me;
        public Arena arena;

        public int hideTile;
        public int targetPlayer = -1;
        public int targetTile;
        public Gadget gadget = Gadget.None;
        public bool locked;

        public int hoverPlayer = -1;
        public int hoverTile = -1;

        public bool Dirty;

        public HumanInput(PlayerState p, Arena a) { me = p; arena = a; }

        // ------------------------------------------------------------------ prep

        public void TickPrep(float dt)
        {
            if (!me.alive || me.avatar == null) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;

            var cam = ArenaCamera.I != null ? ArenaCamera.I.transform : null;
            Vector3 fwd = cam != null ? cam.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.right : Vector3.right;
            fwd.y = 0f; right.y = 0f;
            fwd.Normalize(); right.Normalize();

            Vector3 dir = (right * h + fwd * v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            Vector3 next = me.avatar.transform.position + dir * (5.0f * dt);
            me.avatar.MoveTo(me.zone.Clamp(next), dt);
        }

        // -------------------------------------------------------------- decision

        public void BeginDecision(Vector3 prepEndPos)
        {
            locked = false;
            gadget = Gadget.None;
            hoverPlayer = hoverTile = -1;

            // sensible defaults so a timeout never produces a broken commitment
            var cell = me.zone.CellAt(prepEndPos);
            hideTile = cell != null ? me.zone.IndexOf(cell) : 0;
            if (hideTile < 0) hideTile = 0;

            targetPlayer = -1;
            targetTile = 0;
            Dirty = true;
        }

        public void TickDecision(List<PlayerState> players)
        {
            if (locked || !me.alive) return;

            int hp = -1, ht = -1;
            var cam = ArenaCamera.I != null ? ArenaCamera.I.Cam : Camera.main;
            if (cam == null || !arena.Pick(cam.ScreenPointToRay(Input.mousePosition), out hp, out ht))
            {
                hp = -1;
                ht = -1;
            }

            if (hp != hoverPlayer || ht != hoverTile)
            {
                hoverPlayer = hp;
                hoverTile = ht;
                Dirty = true;
                if (hp >= 0) Audio.Play(Sfx.UiHover, 1f, 0.3f);
            }

            if (Input.GetMouseButtonDown(0) && hp >= 0)
            {
                if (hp == me.index)
                {
                    hideTile = ht;
                    Dirty = true;
                    Audio.Play(Sfx.UiClick, 1.25f, 0.6f);
                }
                else if (players[hp].alive)
                {
                    targetPlayer = hp;
                    targetTile = ht;
                    Dirty = true;
                    Audio.Play(Sfx.UiClick, 0.85f, 0.7f);
                }
            }

            // gadget hotkeys
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Q)) SetGadget(Gadget.None);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetGadget(Gadget.Splash);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetGadget(Gadget.Shield);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetGadget(Gadget.Decoy);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetGadget(Gadget.Scout);

            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) && CanLock(players))
                locked = true;
        }

        public void SetGadget(Gadget g)
        {
            if (g != Gadget.None && !me.Has(g)) { Audio.Play(Sfx.UiBack, 1f, 0.4f); return; }
            gadget = (gadget == g) ? Gadget.None : g;
            Dirty = true;
            Audio.Play(gadget == Gadget.None ? Sfx.UiBack : Sfx.UiClick, 1.1f, 0.55f);
        }

        public bool CanLock(List<PlayerState> players)
        {
            return targetPlayer >= 0 && targetPlayer < players.Count && players[targetPlayer].alive;
        }

        /// Fills in anything the player did not choose, then writes the commitment.
        public void Commit(List<PlayerState> players)
        {
            if (!CanLock(players))
            {
                var pool = new List<int>();
                for (int i = 0; i < players.Count; i++)
                    if (players[i].alive && i != me.index) pool.Add(i);
                if (pool.Count > 0)
                {
                    targetPlayer = pool[Random.Range(0, pool.Count)];
                    targetTile = Random.Range(0, Mathf.Max(1, players[targetPlayer].zone.TileCount));
                }
            }

            if (targetPlayer >= 0)
                targetTile = Mathf.Clamp(targetTile, 0, Mathf.Max(0, players[targetPlayer].zone.TileCount - 1));
            hideTile = Mathf.Clamp(hideTile, 0, Mathf.Max(0, me.zone.TileCount - 1));

            var d = Decision.Empty;
            d.hideTile = hideTile;
            d.targetPlayer = targetPlayer;
            d.targetTile = targetTile;
            d.gadget = gadget;
            d.locked = true;
            me.decision = d;
            locked = true;
        }
    }
}
