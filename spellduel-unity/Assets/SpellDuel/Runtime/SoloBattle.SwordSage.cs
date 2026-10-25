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
        Battle sageVisualBattle;
        GameObject sageGuardVisual,sageArrayVisual;
        float sageGuardUntil,sageArrayUntil;

        void UpdateSwordSage(float dt)
        {
            if (battle == null || Me == null) return;
            UpdateSwordSageBuffVisuals();
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
            bool on = SwordSageHeldVisible() && cam != null;
            if (!on) { if (heldSword) heldSword.SetActive(false); return; }
            if (!heldSword)
            {
                heldSword=SpellFx.CreateSwordSageBlade();heldSword.name="Held Sword";
                heldSword.transform.localScale=Vector3.one*.19f;
                heldSword.GetComponent<SwordSageFlow>().Visible=SwordSageHeldVisible;
            }
            heldSword.SetActive(true);
            heldSword.GetComponent<SwordSageFlow>().Charge=Me.charging!=null&&Me.charging.id=="yijian"?battle.ChargeProgress(Me):0f;
            var t = cam.transform;
            Vector3 at = Hand != null && Hand.HandVisible
                ? cam.ScreenPointToRay(Hand.Palm).GetPoint(0.3f)
                : t.position + t.forward * 0.3f + t.right * 0.12f - t.up * 0.12f;
            heldSword.transform.position = at;
            float su = (Time.time - swingAt) / HeldSwingTime;
            if (su >= 0f && su < 1f)
            {
                // 揮劍：整把劍以手為支點，沿著手劃的方向掃出一個扇形（劍尖從一側掃到另一側，前後各 80°）
                float e = su * su * (3f - 2f * su);
                float th = Mathf.Lerp(-HeldSwingHalf, HeldSwingHalf, e) * Mathf.Deg2Rad;
                var f = (t.forward * 0.85f + t.up * 0.2f).normalized;
                var blade = f * Mathf.Cos(th) + swingDir * Mathf.Sin(th);
                var normal = Vector3.Cross(f, swingDir); if (normal.sqrMagnitude < 1e-4f) normal = -t.forward;
                heldSword.transform.rotation = Quaternion.LookRotation(blade, normal);
                return;
            }
            // 平常：劍尖朝前上方，微微晃動
            float sway = Mathf.Sin(Time.time * 2.3f) * 3f;
            heldSword.transform.rotation = Quaternion.LookRotation(t.forward * 0.6f + t.up * 0.8f, -t.forward) * Quaternion.Euler(sway, 0f, 0f);
        }

        float swingAt = -9f; Vector3 swingDir = Vector3.right;
        public const float HeldSwingTime = 0.32f, HeldSwingHalf = 80f;
        /// <summary>劍來：揮砍了（手在畫面上從 from 劃到 to）→ 手上的長劍跟著掃一個扇形</summary>
        void StartHeldSwing(Vector2 from, Vector2 to)
        {
            if (!heldSword || !heldSword.activeSelf || cam == null) return;
            var t = cam.transform;
            var d = cam.ScreenPointToRay(to).direction - cam.ScreenPointToRay(from).direction;
            d -= Vector3.Project(d, t.forward);
            swingDir = d.sqrMagnitude > 1e-6f ? d.normalized : t.right;
            swingAt = Time.time;
        }

        bool SwordSageHeldVisible()=>battle!=null&&Me!=null&&Me.Alive&&phase==Phase.Fighting&&(Me.swordUntil>battle.now||Me.charging!=null&&Me.charging.id=="yijian");

        // Observe the existing buff events; never write shield values, cooldowns or skill timers.
        void OnSwordSageBuffVisual(string kind,Vector3 at,Color color,string text)
        {
            if(kind!="buff"||Me==null||string.IsNullOrEmpty(text)||Battle.FlatDistance(at,Me.Chest)>.5f)return;
            if(text.Contains("劍罡護體"))sageGuardUntil=Me.shieldUntil;
            if(text.Contains("劍陣"))sageArrayUntil=Me.shieldUntil;
        }
        bool SwordSageShieldVisible(bool formation)=>battle!=null&&Me!=null&&Me.Alive&&phase==Phase.Fighting&&Me.shieldLeft>0&&Me.shieldUntil>battle.now&&(formation?sageArrayUntil:sageGuardUntil)>battle.now;
        void UpdateSwordSageBuffVisuals()
        {
            if(sageVisualBattle!=battle)
            {
                if(sageVisualBattle!=null)sageVisualBattle.OnEvent-=OnSwordSageBuffVisual;
                sageVisualBattle=battle;sageVisualBattle.OnEvent+=OnSwordSageBuffVisual;
                sageGuardUntil=sageArrayUntil=0;
            }
            bool alive=Me.Alive&&phase==Phase.Fighting&&Me.shieldLeft>0&&Me.shieldUntil>battle.now;
            bool guard=alive&&sageGuardUntil>battle.now,formation=alive&&sageArrayUntil>battle.now;
            if(guard&&!sageGuardVisual)
            {
                sageGuardVisual=SpellFx.CreateSwordSageAura(false);
                sageGuardVisual.GetComponent<SwordSageFlow>().Visible=()=>SwordSageShieldVisible(false);
            }
            if(formation&&!sageArrayVisual)
            {
                sageArrayVisual=SpellFx.CreateSwordSageAura(true);
                sageArrayVisual.GetComponent<SwordSageFlow>().Visible=()=>SwordSageShieldVisible(true);
            }
            if(sageGuardVisual)
            {
                sageGuardVisual.SetActive(guard);sageGuardVisual.transform.position=WorldFrame.FromWorld(Me.Feet);
                sageGuardVisual.transform.rotation=Quaternion.Euler(0,Time.time*16,0);
            }
            if(sageArrayVisual)
            {
                sageArrayVisual.SetActive(formation);sageArrayVisual.transform.position=WorldFrame.FromWorld(Me.Feet);
                sageArrayVisual.transform.rotation=Quaternion.Euler(0,-Time.time*22,0);
            }
        }
        void ClearSwordSage()
        {
            if(sageVisualBattle!=null)sageVisualBattle.OnEvent-=OnSwordSageBuffVisual;
            sageVisualBattle=null;sageGuardUntil=sageArrayUntil=0;
            if(heldSword)Destroy(heldSword);if(sageGuardVisual)Destroy(sageGuardVisual);if(sageArrayVisual)Destroy(sageArrayVisual);
            heldSword=sageGuardVisual=sageArrayVisual=null;yujianAcc=.99f;
        }
    }
}
