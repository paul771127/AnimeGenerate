using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本的連續場景與 3D 運鏡（使用者要求）：
    ///   每個地點開打時，場地前方（我面向的那一邊）就看得到一條通道（DungeonCorridor），入口被結界擋住；
    ///   清完這個地點 → 結界打開；選好下一站 → 整個場景（地板、背景、通道）一起往我身後推 3 秒，像沿著通道走過去，
    ///   走到盡頭畫面暗一下，下一個地點的場景就在眼前（主題換成那個地點的）。陷阱迴廊有自己的美術，不用這套。
    /// AR 裡相機是手機本身不能動，所以「運鏡」是把世界往後推。隊友收到 dnode 也會走同一段。
    /// </summary>
    public partial class SoloBattle
    {
        public const float TravelTime = 3.2f, TravelFade = 0.45f;
        DungeonCorridor corridor;
        GameObject sceneRoot;
        float travelStart = -1f, travelFadeK; Vector3 travelDirS; float nextStepAt;
        bool Traveling => travelStart >= 0f;
        bool CorridorOk => dungeon != null && !InGallery && drun != null;

        /// <summary>地點開打：在我面前建一條通道（最後一站＝首領房沒有出口，不建）</summary>
        void BuildCorridor()
        {
            ClearCorridor();
            if (!CorridorOk || drun.AtEnd) return;
            var poly = area.Polygon;
            float fy = poly.Count > 0 ? poly[0].y : area.FloorY;
            var dirS = WorldFrame.DirFromWorld(Fighter.Flat(Me.forward)); dirS.y = 0f;
            if (dirS.sqrMagnitude < 1e-4f && cam) { dirS = cam.transform.forward; dirS.y = 0f; }
            corridor = DungeonCorridor.Create(poly, fy, dirS, drun.Theme);
            corridor.SetVariant(SceneVariant);
        }

        void ClearCorridor()
        {
            if (corridor) Destroy(corridor.gameObject);
            corridor = null;
        }

        /// <summary>這個地點清完：結界打開</summary>
        void CorridorCleared()
        {
            if (!corridor || corridor.Open) return;
            corridor.OpenGate();
            Sfx.Play("charged", 0.7f, 0.8f);
            Say("🚪 前方的通道打開了！", 2.5f);
        }

        /// <summary>選好下一站：開始沿著通道前進（結束後才真的套用下一站）</summary>
        void StartTravel()
        {
            if (Traveling) return;
            travelStart = Time.time; travelFadeK = 0f; nextStepAt = Time.time + 0.4f;
            travelDirS = corridor ? corridor.Dir : (cam ? Fighter.Flat(cam.transform.forward) : Vector3.forward);
            if (corridor && !corridor.Open) corridor.OpenGate();
            if (!sceneRoot) sceneRoot = new GameObject("DungeonSceneRoot");
            sceneRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ParentScenes();
            Say($"➡ 沿著通道前進…「{drun.node.title}」", 2.5f);
            ChatEvent($"🚶 前往「{drun.node.title}」");
        }

        /// <summary>把所有場景與通道掛到同一個根物件下，一起移動</summary>
        void ParentScenes()
        {
            if (!sceneRoot) return;
            void P(Component c) { if (c && c.transform.parent != sceneRoot.transform) c.transform.SetParent(sceneRoot.transform, true); }
            P(volcano); P(forest); P(sea); P(arenaScene); P(canyon); P(corridor);
        }

        void UnparentScenes()
        {
            void U(Component c) { if (c && c.transform.parent) c.transform.SetParent(null, true); }
            U(volcano); U(forest); U(sea); U(arenaScene); U(canyon); U(corridor);
            if (sceneRoot) sceneRoot.transform.position = Vector3.zero;
        }

        /// <summary>每幀：把世界往後推；走到盡頭 → 畫面暗一下、換場景、套用下一站</summary>
        void UpdateTravel()
        {
            if (!Traveling) return;
            float t = Mathf.Clamp01((Time.time - travelStart) / TravelTime);
            float k = t * t * (3f - 2f * t);   // 慢→快→慢
            ParentScenes();
            if (sceneRoot) sceneRoot.transform.position = -travelDirS * (DungeonCorridor.Length * k);
            // 腳步聲與輕微晃動（走路感）
            if (Time.time >= nextStepAt && t < 0.9f) { nextStepAt = Time.time + 0.45f; Sfx.Play("select", 0.25f, 0.6f + (float)runRng.NextDouble() * 0.1f); Haptics.Play(Haptics.Kind.Light); shake = Mathf.Max(shake, 2f); }
            // 最後一段：畫面漸暗（走進黑霧）
            travelFadeK = t > 0.8f ? Mathf.Clamp01((t - 0.8f) / 0.2f) : 0f;
            if (t >= 1f)
            {
                travelStart = -1f;
                UnparentScenes();
                ClearCorridor();
                ApplyNodeNow();
                fadeInUntil = Time.time + TravelFade;   // 到站：從黑暗中亮起來
            }
        }
        float fadeInUntil;

        /// <summary>HUD：走進黑霧／到站亮起的全畫面黑幕</summary>
        void DrawTravelFade(float W, float H)
        {
            float a = Traveling ? travelFadeK : fadeInUntil > Time.time ? (fadeInUntil - Time.time) / TravelFade : 0f;
            if (a <= 0.01f) return;
            GUI.color = new Color(0f, 0f, 0f, a * 0.95f);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void ClearTravel()
        {
            travelStart = -1f; travelFadeK = 0f; fadeInUntil = 0f;
            UnparentScenes();
            ClearCorridor();
            if (sceneRoot) Destroy(sceneRoot);
            sceneRoot = null;
        }
    }
}
