using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 劍仙（劍士的進階職業之一）的畫面與操作：
    ///   劍來：持續時間內，畫面上手的位置握著一把發光的長劍（手不在畫面＝右下角）。
    ///   御劍術：持續 2 秒，每秒射出 N 把飛劍（N＝技能的 multi，技能樹「御劍數量」可以加），每一把都從手的位置飛向準星對準的地方。
    /// </summary>
    public partial class SoloBattle
    {
        GameObject heldSword; float yujianAcc;

        void UpdateSwordSage(float dt)
        {
            if (battle == null || Me == null) return;
            // 御劍術：照準星方向連射飛劍
            if (Me.yujianUntil > battle.now && Me.yujianSkill != null && Me.Alive)
            {
                yujianAcc += dt * Mathf.Max(1, Me.yujianSkill.multi);
                while (yujianAcc >= 1f)
                {
                    yujianAcc -= 1f;
                    var ray = cam.ScreenPointToRay(ScreenCenter);
                    var dir = AimFromHand(ray, out var from);
                    battle.FireFlyingSword(Me, from, dir);
                    Sfx.Play("cast_swordsman", 0.35f, 1.4f);
                }
            }
            else yujianAcc = 0.99f;   // 下次一開始就先射出一把
            UpdateHeldSword();
        }

        /// <summary>劍來：手上的長劍（跟著畫面上手掌的位置）</summary>
        void UpdateHeldSword()
        {
            bool on = battle != null && Me != null && Me.Alive && Me.swordUntil > battle.now && phase == Phase.Fighting && cam != null;
            if (!on) { if (heldSword) heldSword.SetActive(false); return; }
            if (!heldSword)
            {
                heldSword = new GameObject("Held Sword");
                var blade = Prim(PrimitiveType.Cube, new Color(0.75f, 0.92f, 1f, 0.9f), true);
                blade.transform.SetParent(heldSword.transform, false);
                blade.transform.localScale = new Vector3(0.025f, 0.006f, 0.75f); blade.transform.localPosition = new Vector3(0f, 0f, 0.42f);
                var glow = Prim(PrimitiveType.Cube, new Color(0.5f, 0.85f, 1f, 0.35f), true);
                glow.transform.SetParent(heldSword.transform, false);
                glow.transform.localScale = new Vector3(0.05f, 0.012f, 0.78f); glow.transform.localPosition = new Vector3(0f, 0f, 0.42f);
                var guard = Prim(PrimitiveType.Cube, new Color(0.95f, 0.8f, 0.4f), true);
                guard.transform.SetParent(heldSword.transform, false);
                guard.transform.localScale = new Vector3(0.11f, 0.02f, 0.02f); guard.transform.localPosition = new Vector3(0f, 0f, 0.04f);
                var hilt = Prim(PrimitiveType.Cube, new Color(0.35f, 0.25f, 0.2f), true);
                hilt.transform.SetParent(heldSword.transform, false);
                hilt.transform.localScale = new Vector3(0.02f, 0.02f, 0.09f); hilt.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            }
            heldSword.SetActive(true);
            var t = cam.transform;
            Vector3 at = Hand != null && Hand.HandVisible
                ? cam.ScreenPointToRay(Hand.Palm).GetPoint(0.3f)
                : t.position + t.forward * 0.3f + t.right * 0.12f - t.up * 0.12f;
            heldSword.transform.position = at;
            // 劍尖朝前上方，微微晃動
            float sway = Mathf.Sin(Time.time * 2.3f) * 3f;
            heldSword.transform.rotation = Quaternion.LookRotation(t.forward * 0.6f + t.up * 0.8f, -t.forward) * Quaternion.Euler(sway, 0f, 0f);
        }

        void ClearSwordSage() { if (heldSword) heldSword.SetActive(false); yujianAcc = 0.99f; }
    }
}
