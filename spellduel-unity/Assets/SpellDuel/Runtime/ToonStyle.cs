using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 賽璐珞風格的全域參數（角色、小怪、Boss 共用 SpellDuel/Toon 著色器）。
    /// 美術調整只要改這裡的數字（說明見 spellduel-unity/ART_STYLE.md）。
    /// </summary>
    public static class ToonStyle
    {
        public static Color OutlineColor = new Color(0.015f, 0.012f, 0.025f, 1f);   // 黑邊顏色
        public static float OutlineWidth = 2.8f;      // 黑邊粗細（1000 像素高畫面的像素數；實際依螢幕高度放大）
        public static float OutlineFar = 3.2f;        // 超過幾公尺開始變細（遠處的小怪不會變成一團黑）
        public static Color ShadeColor = new Color(0.52f, 0.48f, 0.68f, 1f);    // 暗部＝原色 × 這個顏色（偏藍紫）
        public static float ShadeThreshold = 0.12f;   // 亮暗分界（越大暗部越多）
        public static float ShadeSoftness = 0.018f;    // 分界柔邊（0＝完全硬邊）
        public static Color RimColor = new Color(1f, 0.96f, 0.88f, 0.12f);          // 邊緣亮光（a＝強度）
        public static float Ambient = 0.22f;          // 環境光量

        static Shader toon; static bool tried;

        /// <summary>角色用的受光著色器：賽璐珞；找不到（舊版建置）就退回 Legacy Diffuse</summary>
        public static Shader LitShader()
        {
            if (!tried)
            {
                tried = true;
                toon = Shader.Find("SpellDuel/Toon");
                if (toon != null && !toon.isSupported) toon = null;
                Apply();
            }
            return toon != null ? toon : (Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse"));
        }

        /// <summary>把參數送到著色器（改了數字之後呼叫一次）</summary>
        public static void Apply()
        {
            Shader.SetGlobalColor("_SD_OutlineColor", OutlineColor);
            Shader.SetGlobalFloat("_SD_OutlineWidth", OutlineWidth);
            Shader.SetGlobalFloat("_SD_OutlineFar", OutlineFar);
            Shader.SetGlobalColor("_SD_ShadeColor", ShadeColor);
            Shader.SetGlobalFloat("_SD_ShadeThreshold", ShadeThreshold);
            Shader.SetGlobalFloat("_SD_ShadeSoftness", ShadeSoftness);
            Shader.SetGlobalColor("_SD_RimColor", RimColor);
            Shader.SetGlobalFloat("_SD_Ambient", Ambient);
            Shader.SetGlobalColor("_SD_EnvTint", Color.white);   // AR 光照估計開始前：不變色（ArLighting 每幀覆寫）
        }
    }
}
