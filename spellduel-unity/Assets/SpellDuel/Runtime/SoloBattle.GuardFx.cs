using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 防禦狀態的第一人稱光盾（眼前 0.6m，跟著鏡頭）：
    ///   格擋中＝藍色六角盾、護盾（鐵壁等）＝鋼色雙層六角、反擊中＝旋轉的粉紅鋸齒環；
    ///   擋下一擊時整個光盾亮一下、放大一點。劍仙的劍罡護體／劍陣有自己的外觀，不重複畫。
    /// </summary>
    public partial class SoloBattle
    {
        GameObject guardFx; LineRenderer guardHex, guardHexIn, guardZig; float guardFlashAt = -9f;

        void UpdateGuardFx()
        {
            if (battle == null || Me == null || cam == null || phase != Phase.Fighting || !Me.Alive) { if (guardFx) guardFx.SetActive(false); return; }
            float now = battle.now;
            bool block = Me.blockUntil > now, counter = Me.counterUntil > now;
            bool shield = Me.shieldUntil > now && Me.shieldLeft > 0 && sageGuardUntil <= now && sageArrayUntil <= now;
            if (!block && !counter && !shield) { if (guardFx) guardFx.SetActive(false); return; }
            if (!guardFx)
            {
                guardFx = new GameObject("Guard FX");
                guardFx.transform.SetParent(cam.transform, false);
                guardFx.transform.localPosition = new Vector3(0f, -0.05f, 0.6f);
                guardHex = SpellFx.Line(guardFx.transform, "Hex", SpellFx.Polygon(6, 0.17f, 90f), true, 0.007f, Color.white, true, true);
                guardHexIn = SpellFx.Line(guardFx.transform, "HexIn", SpellFx.Polygon(6, 0.12f, 90f), true, 0.004f, Color.white, true, true);
                guardZig = SpellFx.Line(guardFx.transform, "Zig", SpellFx.Zigzag(30, 0.19f, 0.215f), true, 0.005f, Color.white, true, true);
            }
            guardFx.SetActive(true);
            float flash = Mathf.Clamp01(1f - (Time.time - guardFlashAt) / 0.25f);   // 擋下一擊：亮一下
            float pulse = 0.3f + 0.1f * Mathf.Sin(Time.time * 5f) + 0.6f * flash;
            guardFx.transform.localScale = Vector3.one * (1f + 0.12f * flash);
            guardFx.transform.localRotation = Quaternion.identity;
            var hexCol = shield ? new Color(0.8f, 0.86f, 0.95f) : new Color(0.45f, 0.72f, 1f);
            void Set(LineRenderer lr, bool on, Color c, float a) { lr.enabled = on; if (on) { c.a = a; lr.startColor = lr.endColor = c; } }
            Set(guardHex, block || shield, hexCol, pulse);
            Set(guardHexIn, shield, hexCol, pulse * 0.8f);
            Set(guardZig, counter, new Color(0.98f, 0.45f, 0.7f), pulse + 0.1f);
            if (counter) guardZig.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 140f);
        }

        void ClearGuardFx() { if (guardFx) Destroy(guardFx); guardFx = null; }
    }
}
