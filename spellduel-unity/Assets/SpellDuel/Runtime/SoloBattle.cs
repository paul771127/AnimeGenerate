using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 單人對戰的畫面與操作（邏輯在 Battle / EnemyAI，這裡只負責顯示與輸入）。
    ///   開戰前：選我的職業與 3 個技能、敵人職業（可隨機）。
    ///   戰鬥中：點技能按鈕開始詠唱 → 蓄力完成後點畫面朝該方向發射（陷阱設在點到的地板）。
    ///   敵人、法術、陷阱都放在場地座標裡，用 AR 相機看到的是它們在房間裡的真實位置。
    /// </summary>
    public class SoloBattle : MonoBehaviour
    {
        public enum Phase { Hidden, Setup, Fighting, Over }
        public Phase phase { get; private set; } = Phase.Hidden;
        public Action RequestRedraw, RequestChangeMode;

        Camera cam;
        PlayArea area;
        Material mat;
        Battle battle;
        EnemyAI ai;

        // 選擇（存在 PlayerPrefs）
        string myClass, enemyClass;           // enemyClass = "random" 或職業 id
        readonly List<string> myLoadout = new List<string>();
        string enemyClassUsed;

        // 顯示物件
        GameObject enemyBody, enemyHead, enemyNose, enemyShield;
        readonly Dictionary<Projectile, GameObject> projGo = new Dictionary<Projectile, GameObject>();
        readonly Dictionary<Trap, GameObject> trapGo = new Dictionary<Trap, GameObject>();
        class Floater { public Vector3 posMap; public string text; public Color color; public float born; public bool screen; }
        readonly List<Floater> floaters = new List<Floater>();
        string message = ""; float messageUntil;
        float incomingFlash, hitFlash;
        // AR 追蹤中斷：和雙人模式相同——戰鬥照常進行，我的身體停在最後的正確位置，暫時不能施法
        bool frozen;
        // 鎖定：敵人出現在我的畫面中才能發射攻擊法術（和雙人模式相同）
        bool enemyOnScreen; Rect enemyRect; float enemyScreenSide;   // enemyScreenSide：敵人在左(<0)／右(>0)
        GUIStyle label, small, button, big, center;

        Fighter Me => battle?.player;
        Fighter En => battle?.enemy;

        public void Init(Camera camera, PlayArea playArea, Material baseMat)
        {
            cam = camera; area = playArea; mat = baseMat;
            myClass = PlayerPrefs.GetString("sd_my_class", "mage");
            if (!Skills.Classes.ContainsKey(myClass)) myClass = "mage";
            enemyClass = PlayerPrefs.GetString("sd_enemy_class", "random");
            LoadLoadout();
        }

        void LoadLoadout()
        {
            myLoadout.Clear();
            var saved = PlayerPrefs.GetString("sd_loadout_" + myClass, "");
            var cls = Skills.Classes[myClass];
            foreach (var id in saved.Split(',')) if (Array.IndexOf(cls.skills, id) >= 0 && myLoadout.Count < 3) myLoadout.Add(id);
            if (myLoadout.Count != 3) { myLoadout.Clear(); myLoadout.AddRange(cls.defaultLoadout); }
        }

        void SaveChoices()
        {
            PlayerPrefs.SetString("sd_my_class", myClass);
            PlayerPrefs.SetString("sd_enemy_class", enemyClass);
            PlayerPrefs.SetString("sd_loadout_" + myClass, string.Join(",", myLoadout));
        }

        public void ShowSetup() { EndBattle(); phase = Phase.Setup; }
        public void Hide() { EndBattle(); phase = Phase.Hidden; }

        // ================================================================ 開戰
        void StartBattle()
        {
            SaveChoices();
            EndBattle();
            enemyClassUsed = enemyClass == "random" ? Skills.ClassOrder[UnityEngine.Random.Range(0, Skills.ClassOrder.Length)] : enemyClass;
            var ec = Skills.Classes[enemyClassUsed];
            // 敵人隨機帶 3 個技能（至少 2 個攻擊）
            var attacks = new List<string>(); var guards = new List<string>();
            foreach (var id in ec.skills) (Skills.All[id].type == SkillType.Self ? guards : attacks).Add(id);
            Shuffle(attacks); Shuffle(guards);
            var enemySkills = new List<string>(attacks.GetRange(0, Mathf.Min(2, attacks.Count)));
            var rest = new List<string>(attacks.GetRange(enemySkills.Count, attacks.Count - enemySkills.Count)); rest.AddRange(guards); Shuffle(rest);
            if (rest.Count > 0) enemySkills.Add(rest[0]);

            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            var en = new Fighter($"電腦{ec.name}", ec, enemySkills, false);
            // 敵人出生點：場地內、玩家面向的那一側
            float reach = area.ReachInside(area.Origin.forward, 0.6f);
            en.head = new Vector3(0f, 1.6f, Mathf.Clamp(reach, 0f, 3f));
            SyncPlayer(me);
            battle = new Battle(me, en);
            battle.OnEvent += OnBattleEvent;
            ai = new EnemyAI(battle, en, p => area.Inside(WorldFrame.FromWorld(p)), p => area.DistanceToEdge(WorldFrame.FromWorld(p)), Environment.TickCount);

            enemyBody = Prim(PrimitiveType.Capsule, WithAlpha(ec.color, 0.75f));
            enemyHead = Prim(PrimitiveType.Sphere, WithAlpha(ec.color, 0.9f));
            enemyNose = Prim(PrimitiveType.Cube, new Color(1f, 1f, 1f, 0.9f));
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            phase = Phase.Fighting;
            Say($"⚔ 對手：{en.name}（{string.Join("・", enemySkills.ConvertAll(id => Skills.All[id].name))}）", 4f);
        }

        void EndBattle()
        {
            foreach (var go in projGo.Values) if (go) Destroy(go);
            foreach (var go in trapGo.Values) if (go) Destroy(go);
            projGo.Clear(); trapGo.Clear(); floaters.Clear();
            foreach (var go in new[] { enemyBody, enemyHead, enemyNose, enemyShield }) if (go) Destroy(go);
            battle = null; ai = null;
        }

        static void Shuffle<T>(List<T> l) { for (int i = l.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); } }

        // ================================================================ 每幀
        void Update()
        {
            if (phase != Phase.Fighting && phase != Phase.Over) return;
            if (battle == null) return;
            SyncPlayer(Me);
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 2.5f);
            frozen = phase == Phase.Fighting && !Tracking.Ok;
            UpdateLock();
            if (phase == Phase.Fighting)
            {
                ai.Update(dt);
                battle.Update(dt);
                HandleInput();
                if (battle.Over) phase = Phase.Over;
            }
            UpdateVisuals();
        }

        // 玩家的位置＝手機位置（換成場地座標）；追蹤中斷時位置不可信 → 停在最後的正確位置
        void SyncPlayer(Fighter me)
        {
            if (!Tracking.Ok) return;
            me.head = WorldFrame.ToWorld(cam.transform.position);
            me.forward = WorldFrame.DirToWorld(cam.transform.forward);
        }

        bool InTapZone(Vector2 guiPos) => guiPos.y > Screen.height * 0.2f && guiPos.y < Screen.height * 0.74f;

        void HandleInput()
        {
            if (!Input.GetMouseButtonDown(0) || GUIUtility.hotControl != 0) return;
            var sp = Input.mousePosition;
            if (frozen) { Say("AR 追蹤中斷，暫時不能施法", 1.5f); return; }
            if (!InTapZone(new Vector2(sp.x, Screen.height - sp.y))) return;
            if (Me.charging == null) { Say("先點下方的技能開始詠唱", 1.5f); return; }
            if (Me.charging.type == SkillType.Projectile && !enemyOnScreen && battle.ChargeProgress(Me) >= 1f)
            { Say("🎯 敵人不在畫面中，轉向敵人才能鎖定", 1.5f); return; }

            // 點擊方向（場地座標）與地板交點（陷阱用）
            var ray = cam.ScreenPointToRay(sp);
            var o = WorldFrame.ToWorld(ray.origin);
            var d = WorldFrame.DirToWorld(ray.direction);
            var floor = d.y < -0.01f ? o + d * (-o.y / d.y) : Me.Feet + Fighter.Flat(Me.forward) * 1.5f;
            if (!battle.TryRelease(Me, d, floor, out var why)) Say(why, 1.5f);
        }

        /// <summary>敵人有沒有出現在我的畫面中（頭、胸、腳任一處），並算出鎖定框</summary>
        void UpdateLock()
        {
            enemyOnScreen = false;
            var en = En;
            if (en == null || !en.Alive || !Tracking.Ok) return;
            var pts = new[] { en.head + Vector3.up * 0.15f, en.Chest, en.Feet };
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            int inView = 0;
            foreach (var pm in pts)
            {
                var p = WorldFrame.FromWorld(pm);
                var v = cam.WorldToViewportPoint(p);
                if (v.z < 0.2f) continue;
                if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) inView++;
                var sp = cam.WorldToScreenPoint(p);
                xMin = Mathf.Min(xMin, sp.x); xMax = Mathf.Max(xMax, sp.x);
                yMin = Mathf.Min(yMin, sp.y); yMax = Mathf.Max(yMax, sp.y);
            }
            enemyOnScreen = inView > 0;
            // 不在畫面中：敵人在我的左邊還是右邊
            var toEn = WorldFrame.FromWorld(en.Chest) - cam.transform.position;
            enemyScreenSide = Vector3.Dot(toEn, cam.transform.right);
            if (enemyOnScreen)
            {
                float depth = Mathf.Max(0.3f, Vector3.Dot(toEn, cam.transform.forward));
                float halfW = Fighter.BodyRadius / depth * Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                float cx = (xMin + xMax) * 0.5f;
                enemyRect = Rect.MinMaxRect(cx - halfW, Screen.height - yMax, cx + halfW, Screen.height - yMin);
            }
        }

        void OnBattleEvent(string kind, Vector3 at, Color c, string text)
        {
            switch (kind)
            {
                case "hit": case "trap": case "mitigate": case "effect": case "counter": case "buff": case "heal": case "trapSet":
                    if (!string.IsNullOrEmpty(text)) floaters.Add(new Floater { posMap = at, text = text, color = c, born = Time.time });
                    if (kind == "hit" || kind == "trap") Burst(at, c);
                    break;
                case "hurt":
                    // 被打中：震動＋整個畫面閃紅＋扣血（血量已在 Battle 裡扣掉）
                    floaters.Add(new Floater { text = text, color = new Color(1f, 0.3f, 0.3f), born = Time.time, screen = true });
                    hitFlash = 1f;
                    Handheld.Vibrate();
                    break;
                case "dot":
                    bool onMe = at == Me.Chest;
                    floaters.Add(new Floater { posMap = at, text = "毒 " + text, color = Color.green, born = Time.time, screen = onMe });
                    if (onMe) hitFlash = Mathf.Max(hitFlash, 0.4f);   // 中毒扣血：淡一點的紅
                    break;
                case "miss":
                    break;
                case "timeout":
                    if (at == Me.Chest) Say("詠唱逾時", 1.5f);
                    break;
                case "win": Say("🏆 勝利！", 99f); break;
                case "lose": Say("💀 敗北…", 99f); break;
            }
        }

        // ================================================================ 3D 顯示
        void UpdateVisuals()
        {
            var en = En;
            // 敵人：膠囊身體＋頭＋朝向的「鼻子」，位置換回 AR 空間
            var headS = WorldFrame.FromWorld(en.head);
            var feetS = WorldFrame.FromWorld(en.Feet);
            float h = en.head.y;
            enemyBody.transform.position = (headS + feetS) / 2f + Vector3.down * 0.08f;
            enemyBody.transform.rotation = Quaternion.identity;
            enemyBody.transform.localScale = new Vector3(Fighter.BodyRadius * 2f, (h - 0.15f) / 2f, Fighter.BodyRadius * 2f);
            enemyHead.transform.position = headS;
            enemyHead.transform.localScale = Vector3.one * 0.26f;
            var fwdS = WorldFrame.DirFromWorld(en.forward);
            enemyNose.transform.position = headS + fwdS * 0.14f;
            enemyNose.transform.rotation = Quaternion.LookRotation(fwdS == Vector3.zero ? Vector3.forward : fwdS, Vector3.up);
            enemyNose.transform.localScale = new Vector3(0.1f, 0.05f, 0.08f);
            bool guarded = en.blockUntil > battle.now || en.shieldUntil > battle.now || en.counterUntil > battle.now;
            enemyShield.SetActive(guarded && en.Alive);
            if (guarded)
            {
                enemyShield.transform.position = WorldFrame.FromWorld(en.Chest);
                enemyShield.transform.localScale = new Vector3(0.9f, 1.9f, 0.9f);
                var col = en.counterUntil > battle.now ? new Color(0.96f, 0.45f, 0.71f, 0.3f) : en.shieldUntil > battle.now ? new Color(0.6f, 0.65f, 0.7f, 0.3f) : new Color(0.38f, 0.65f, 0.98f, 0.3f);
                enemyShield.GetComponent<Renderer>().material.color = col;
            }
            if (!en.Alive) { enemyBody.SetActive(false); enemyHead.SetActive(false); enemyNose.SetActive(false); }

            // 法術：球體＋拖尾
            var alive = new HashSet<Projectile>(battle.projectiles);
            foreach (var p in battle.projectiles)
            {
                if (!projGo.TryGetValue(p, out var go))
                {
                    go = Prim(PrimitiveType.Sphere, WithAlpha(p.skill.color, 0.95f));
                    go.transform.localScale = Vector3.one * Mathf.Max(0.06f, p.skill.radius * 2f);
                    var tr = go.AddComponent<TrailRenderer>();
                    tr.time = 0.2f; tr.startWidth = Mathf.Max(0.04f, p.skill.radius * 1.5f); tr.endWidth = 0;
                    tr.material = mat; tr.startColor = WithAlpha(p.skill.color, 0.8f); tr.endColor = WithAlpha(p.skill.color, 0f);
                    projGo[p] = go;
                }
                go.transform.position = WorldFrame.FromWorld(p.pos);
                // 敵人的法術逼近時畫面邊框閃紅
                if (p.owner == En && Vector3.Distance(p.pos, Me.Chest) < 2.5f && Vector3.Dot(p.dir, Me.Chest - p.pos) > 0) incomingFlash = Mathf.Max(incomingFlash, 0.6f);
            }
            foreach (var kv in new List<KeyValuePair<Projectile, GameObject>>(projGo))
                if (!alive.Contains(kv.Key)) { Destroy(kv.Value, 0.25f); projGo.Remove(kv.Key); }

            // 陷阱：地板上的扁圓盤（生效前是灰色）
            var liveTraps = new HashSet<Trap>(battle.traps);
            foreach (var t in battle.traps)
            {
                if (!trapGo.TryGetValue(t, out var go))
                {
                    go = Prim(PrimitiveType.Cylinder, Color.gray);
                    go.transform.localScale = new Vector3(t.skill.radius * 2f, 0.005f, t.skill.radius * 2f);
                    trapGo[t] = go;
                }
                go.transform.position = WorldFrame.FromWorld(t.pos + Vector3.up * 0.01f);
                go.GetComponent<Renderer>().material.color = t.Armed(battle.now) ? WithAlpha(t.skill.color, 0.6f) : new Color(0.6f, 0.6f, 0.6f, 0.4f);
            }
            foreach (var kv in new List<KeyValuePair<Trap, GameObject>>(trapGo))
                if (!liveTraps.Contains(kv.Key)) { Burst(kv.Key.pos, kv.Key.skill.color); Destroy(kv.Value); trapGo.Remove(kv.Key); }

            incomingFlash = Mathf.Max(0f, incomingFlash - Time.deltaTime * 2f);
        }

        void Burst(Vector3 posMap, Color c)
        {
            var go = Prim(PrimitiveType.Sphere, WithAlpha(c, 0.8f));
            go.transform.position = WorldFrame.FromWorld(posMap);
            StartCoroutine(BurstAnim(go, c));
        }

        System.Collections.IEnumerator BurstAnim(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            for (float t = 0; t < 0.35f && go; t += Time.deltaTime)
            {
                go.transform.localScale = Vector3.one * (0.1f + t * 2.2f);
                r.material.color = WithAlpha(c, 0.8f * (1f - t / 0.35f));
                yield return null;
            }
            if (go) Destroy(go);
        }

        GameObject Prim(PrimitiveType type, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = mat;
            r.material.color = c;
            return go;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
        void Say(string s, float secs) { message = s; messageUntil = Time.time + secs; }

        // ================================================================ 介面（GameRoot 在單人場地完成後呼叫）
        void Styles()
        {
            if (label != null) return;
            int fs = Mathf.RoundToInt(Screen.height / 46f);
            label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true }; label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(fs * 0.8f) };
            button = new GUIStyle(GUI.skin.button) { fontSize = fs, wordWrap = true };
            big = new GUIStyle(label) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        }

        public void DrawGUI()
        {
            Styles();
            if (phase == Phase.Setup) DrawSetup();
            else if (phase == Phase.Fighting || phase == Phase.Over) DrawHud();
        }

        void Panel(Rect r, float alpha = 0.6f)
        {
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawSetup()
        {
            float W = Screen.width, H = Screen.height, pad = W * 0.03f, lh = label.fontSize * 1.7f;
            float top = H - Screen.safeArea.yMax + pad;
            Panel(new Rect(0, 0, W, H), 0.55f);
            float y = top;
            GUI.Label(new Rect(pad, y, W, lh), "我的職業", label); y += lh;
            float bw = (W - pad * 5) / 4f, bh = lh * 1.3f;
            for (int i = 0; i < 4; i++)
            {
                var id = Skills.ClassOrder[i];
                GUI.color = id == myClass ? Skills.Classes[id].color : Color.white;
                if (GUI.Button(new Rect(pad + i * (bw + pad), y, bw, bh), Skills.Classes[id].name, button) && id != myClass) { myClass = id; LoadLoadout(); }
            }
            GUI.color = Color.white; y += bh + pad * 0.5f;
            var cls = Skills.Classes[myClass];
            GUI.Label(new Rect(pad, y, W - pad * 2, lh * 1.4f), $"{cls.desc}　HP {cls.maxHp}・MP {cls.maxMp}", small); y += lh * 1.2f;

            GUI.Label(new Rect(pad, y, W, lh), $"選 3 個技能（{myLoadout.Count}/3）", label); y += lh;
            float sw = (W - pad * 3) / 2f, sh = lh * 2.1f;
            for (int i = 0; i < cls.skills.Length; i++)
            {
                var s = Skills.All[cls.skills[i]];
                bool on = myLoadout.Contains(s.id);
                GUI.color = on ? s.color : new Color(0.75f, 0.75f, 0.75f);
                var r = new Rect(pad + (i % 2) * (sw + pad), y + (i / 2) * (sh + pad * 0.5f), sw, sh);
                string eff = s.type == SkillType.Self ? (s.self == SelfKind.Heal ? $"回復{s.heal}" : "防禦") : $"傷害{s.damage}{(s.multi > 1 ? $"×{s.multi}" : "")}";
                if (GUI.Button(r, $"{(on ? "✔ " : "")}{s.name}\nMP{s.cost}・蓄力{s.charge:0.#}s・{eff}・{s.RangeText}", button))
                {
                    if (on) myLoadout.Remove(s.id);
                    else { if (myLoadout.Count >= 3) myLoadout.RemoveAt(0); myLoadout.Add(s.id); }
                }
            }
            GUI.color = Color.white;
            y += ((cls.skills.Length + 1) / 2) * (sh + pad * 0.5f) + pad * 0.5f;

            GUI.Label(new Rect(pad, y, W, lh), "敵人職業", label); y += lh;
            float ew = (W - pad * 6) / 5f;
            for (int i = 0; i < 5; i++)
            {
                string id = i == 0 ? "random" : Skills.ClassOrder[i - 1];
                string nm = i == 0 ? "隨機" : Skills.Classes[id].name;
                GUI.color = id == enemyClass ? Color.yellow : Color.white;
                if (GUI.Button(new Rect(pad + i * (ew + pad), y, ew, bh), nm, button)) enemyClass = id;
            }
            GUI.color = Color.white; y += bh + pad;

            GUI.enabled = myLoadout.Count == 3;
            if (GUI.Button(new Rect(pad, y, W - pad * 2, bh * 1.3f), "⚔ 開始戰鬥", button)) StartBattle();
            GUI.enabled = true; y += bh * 1.3f + pad;
            if (GUI.Button(new Rect(pad, y, (W - pad * 3) / 2f, bh), "重畫場地", button)) RequestRedraw?.Invoke();
            if (GUI.Button(new Rect(pad * 2 + (W - pad * 3) / 2f, y, (W - pad * 3) / 2f, bh), "換模式", button)) RequestChangeMode?.Invoke();
        }

        void DrawHud()
        {
            float W = Screen.width, H = Screen.height, pad = W * 0.03f, lh = label.fontSize * 1.6f;
            float top = H - Screen.safeArea.yMax + pad;
            var me = Me; var en = En; float now = battle.now;

            // 被打中：整個畫面閃紅
            if (hitFlash > 0.01f) { GUI.color = new Color(1f, 0f, 0.05f, hitFlash * 0.45f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }

            // 致盲：畫面蓋上煙霧
            if (me.blindUntil > now) { GUI.color = new Color(0.55f, 0.55f, 0.6f, 0.92f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; GUI.Label(new Rect(0, H * 0.3f, W, lh * 2), $"煙霧中… {me.blindUntil - now:F1}s", big); }
            // 敵人法術逼近：邊框閃紅
            if (incomingFlash > 0.01f)
            {
                GUI.color = new Color(1f, 0.15f, 0.2f, incomingFlash * 0.8f);
                float t = W * 0.025f;
                GUI.DrawTexture(new Rect(0, 0, W, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - t, W, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, t, H), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(W - t, 0, t, H), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // 上方：敵人資訊
            Panel(new Rect(0, 0, W, top + lh * 3.2f));
            float y = top;
            GUI.Label(new Rect(pad, y, W * 0.6f, lh), $"{en.name}　距離 {battle.Distance:F1}m", label);
            Bar(new Rect(W * 0.6f, y + lh * 0.25f, W * 0.37f, lh * 0.5f), en.hp / en.maxHp, new Color(1f, 0.25f, 0.35f), $"{Mathf.CeilToInt(en.hp)}");
            y += lh;
            string enState = en.charging != null ? $"⚠ 詠唱 {en.charging.name}（{Mathf.FloorToInt(battle.ChargeProgress(en) * 100)}%）" : ai.Status;
            if (en.snaredUntil > now) enState = "被定身";
            GUI.Label(new Rect(pad, y, W - pad * 2, lh), enState, label); y += lh;
            GUI.Label(new Rect(pad, y, W - pad * 2, lh), StatusText(en, now), small);

            // 敵人頭上的血條
            var hs = cam.WorldToScreenPoint(WorldFrame.FromWorld(en.head + Vector3.up * 0.3f));
            if (hs.z > 0 && en.Alive)
            {
                var r = new Rect(hs.x - W * 0.12f, H - hs.y, W * 0.24f, lh * 0.4f);
                Bar(r, en.hp / en.maxHp, new Color(1f, 0.25f, 0.35f), "");
                if (en.charging != null) GUI.Label(new Rect(r.x - W * 0.1f, r.y - lh, r.width + W * 0.2f, lh), $"⚠ {en.charging.name}", center);
            }

            // 飄字
            foreach (var f in floaters)
            {
                float age = Time.time - f.born;
                if (age > 1.2f) continue;
                Vector2 p;
                if (f.screen) p = new Vector2(W / 2, H * 0.45f - age * 60f);
                else
                {
                    var s = cam.WorldToScreenPoint(WorldFrame.FromWorld(f.posMap));
                    if (s.z <= 0) continue;
                    p = new Vector2(s.x, H - s.y - age * 60f);
                }
                GUI.color = new Color(f.color.r, f.color.g, f.color.b, 1f - age / 1.2f);
                GUI.Label(new Rect(p.x - W * 0.3f, p.y - lh, W * 0.6f, lh * 1.5f), f.text, center);
            }
            GUI.color = Color.white;
            floaters.RemoveAll(f => Time.time - f.born > 1.2f);

            // 準星與蓄力
            if (me.charging != null)
            {
                float prog = battle.ChargeProgress(me);
                int rs = battle.RangeState(me, me.charging);
                string st = prog < 1f ? $"蓄力 {Mathf.FloorToInt(prog * 100)}%" :
                    (me.charging.releaseNear && rs > 0) ? "靠近才能出手" : rs < 0 ? "太近了" :
                    me.charging.type == SkillType.Trap ? "點地板設置陷阱" : me.charging.type == SkillType.Self ? "點畫面發動" : "點畫面發射！";
                GUI.color = prog < 1f ? Color.white : me.charging.color;
                GUI.Label(new Rect(0, H / 2 - lh * 2.2f, W, lh), $"{me.charging.name}　{st}", center);
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(W / 2 - 50, H / 2 - 50, 100, 100), "＋", big);

            // 鎖定框／敵人方向提示
            if (phase == Phase.Fighting && en.Alive && !frozen)
            {
                if (enemyOnScreen)
                {
                    float t = Mathf.Max(3f, W * 0.006f);
                    GUI.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                    var r = enemyRect;
                    GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(r.x, r.yMax, Mathf.Max(r.width, W * 0.3f), lh), "🎯 鎖定", small);
                }
                else
                {
                    string arrow = enemyScreenSide < 0 ? "◀ 敵人在左邊" : "敵人在右邊 ▶";
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    GUI.Label(new Rect(0, H * 0.56f, W, lh * 1.5f), arrow + "（轉向敵人才能發射）", center);
                    GUI.color = Color.white;
                }
            }
            if (Time.time < messageUntil) GUI.Label(new Rect(0, H * 0.6f, W, lh * 1.5f), message, center);

            // 下方：我的狀態與技能
            float bottomH = lh * 2.4f + W * 0.2f;
            float by = H - (H - Screen.safeArea.yMax) - Screen.safeArea.y - bottomH;
            by = Mathf.Min(by, H * 0.76f);
            Panel(new Rect(0, by - pad * 0.5f, W, H - by + pad));
            Bar(new Rect(pad, by, W - pad * 2, lh * 0.5f), me.hp / me.maxHp, new Color(1f, 0.3f, 0.35f), $"HP {Mathf.CeilToInt(me.hp)}");
            Bar(new Rect(pad, by + lh * 0.6f, W - pad * 2, lh * 0.5f), me.mp / me.maxMp, new Color(0.3f, 0.6f, 1f), $"MP {Mathf.FloorToInt(me.mp)}");
            GUI.Label(new Rect(pad, by + lh * 1.15f, W - pad * 2, lh), StatusText(me, now), small);
            float sw = (W - pad * 4) / 3f, sy = by + lh * 2.1f, sh = W * 0.17f;
            for (int i = 0; i < me.loadout.Count; i++)
            {
                var s = me.loadout[i];
                var r = new Rect(pad + i * (sw + pad), sy, sw, sh);
                float cd = me.cooldownUntil.TryGetValue(s.id, out var u) ? Mathf.Max(0, u - now) : 0;
                bool charging = me.charging == s;
                GUI.color = charging ? s.color : (me.mp < s.cost || cd > 0 ? new Color(0.6f, 0.6f, 0.6f) : Color.white);
                if (GUI.Button(r, $"{s.name}\nMP {s.cost}{(cd > 0 ? $"　{cd:F1}s" : "")}", button) && phase == Phase.Fighting)
                {
                    if (frozen) Say("AR 追蹤中斷，暫時不能施法", 1.5f); else
                    if (!battle.TryChant(me, s, out var why)) Say(why, 1.5f);
                }
                GUI.color = Color.white;
            }

            if (frozen)
            {
                Panel(new Rect(0, H * 0.32f, W, H * 0.2f), 0.8f);
                GUI.Label(new Rect(0, H * 0.33f, W, H * 0.08f), "⚠ AR 追蹤中斷", big);
                GUI.Label(new Rect(pad, H * 0.42f, W - pad * 2, lh * 2), Tracking.Reason + "\n暫時不能施法；敵人仍以你最後的位置攻擊", center);
            }

            if (phase == Phase.Over)
            {
                Panel(new Rect(0, H * 0.3f, W, H * 0.3f), 0.75f);
                GUI.Label(new Rect(0, H * 0.32f, W, H * 0.1f), me.Alive ? "🏆 勝利！" : "💀 敗北…", big);
                float bw = (W - pad * 3) / 2f;
                if (GUI.Button(new Rect(pad, H * 0.47f, bw, lh * 2f), "再來一局", button)) StartBattle();
                if (GUI.Button(new Rect(pad * 2 + bw, H * 0.47f, bw, lh * 2f), "換職業", button)) ShowSetup();
            }
        }

        string StatusText(Fighter f, float now)
        {
            var parts = new List<string>();
            if (f.blockUntil > now) parts.Add($"格擋 {f.blockUntil - now:F1}s");
            if (f.shieldUntil > now) parts.Add($"護盾 {f.shieldLeft}");
            if (f.counterUntil > now) parts.Add($"反擊 {f.counterUntil - now:F1}s");
            if (f.dots.Count > 0) parts.Add("中毒");
            if (f.snaredUntil > now) parts.Add($"定身 {f.snaredUntil - now:F1}s");
            if (f.blindUntil > now) parts.Add("致盲");
            int traps = 0; foreach (var t in battle.traps) if (t.owner == f) traps++;
            if (traps > 0) parts.Add($"陷阱×{traps}");
            return string.Join("　", parts);
        }

        void Bar(Rect r, float v, Color c, string text)
        {
            GUI.color = new Color(1, 1, 1, 0.2f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(r.x + 4, r.y - r.height * 0.6f, r.width, r.height * 2f), text, small);
        }
    }
}
