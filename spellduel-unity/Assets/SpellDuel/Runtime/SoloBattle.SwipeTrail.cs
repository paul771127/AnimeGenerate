using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 揮砍殘影（劍士、初心者的手掌劃線普攻）：手在鏡頭前移動的當下，眼前約 0.6m 跟著拖出一道殘影（像真的在揮刀），
    /// 不用等劃完才看到刀線。只畫最近 0.22 秒、手移動夠快的部分；手停下來、握拳、詠唱中就慢慢淡掉。
    /// 判定不變（照舊是劃完後的刀線）。
    /// </summary>
    public partial class SoloBattle
    {
        LineRenderer swipeTrail;
        readonly List<(Vector3 p, float t)> trailPts = new List<(Vector3, float)>();
        Vector2 trailLastPalm; float trailLastT;
        const float TrailLife = 0.22f, TrailDist = 0.6f, TrailMinSpeed = 0.6f;   // 最短速度：每秒移動螢幕寬的幾倍

        void UpdateSwipeTrail(bool active)
        {
            float now = Time.time;
            if (active && cam != null)
            {
                var palm = Hand.Palm;
                float dt = Mathf.Max(1e-3f, now - trailLastT);
                float speed = (palm - trailLastPalm).magnitude / Mathf.Max(1f, Screen.width) / dt;
                trailLastPalm = palm; trailLastT = now;
                if (speed >= TrailMinSpeed || trailPts.Count > 0 && now - trailPts[trailPts.Count - 1].t < 0.06f)
                {
                    var ray = cam.ScreenPointToRay(palm);
                    trailPts.Add((ray.origin + ray.direction * TrailDist, now));
                }
            }
            trailPts.RemoveAll(q => now - q.t > TrailLife);
            if (trailPts.Count < 2) { if (swipeTrail) swipeTrail.enabled = false; return; }
            if (!swipeTrail)
            {
                var go = new GameObject("Swipe Trail");
                swipeTrail = go.AddComponent<LineRenderer>();
                swipeTrail.material = new Material(Shader.Find("Sprites/Default"));
                swipeTrail.useWorldSpace = true; swipeTrail.numCapVertices = 3;
                swipeTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));   // 尾巴細、手的位置粗
            }
            var col = myClass == Skills.Novice ? new Color(0.85f, 0.62f, 0.35f) : Color.Lerp(Skills.Classes[myClass].color, Color.white, 0.5f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(col, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 1f) });
            swipeTrail.colorGradient = g;
            swipeTrail.widthMultiplier = 0.035f;
            swipeTrail.enabled = true;
            swipeTrail.positionCount = trailPts.Count;
            for (int i = 0; i < trailPts.Count; i++) swipeTrail.SetPosition(i, trailPts[i].p);
        }

        void ClearSwipeTrail() { trailPts.Clear(); if (swipeTrail) swipeTrail.enabled = false; }
    }
}
