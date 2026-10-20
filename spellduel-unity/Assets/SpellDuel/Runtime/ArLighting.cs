using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace SpellDuel
{
    /// <summary>
    /// AR 光照估計：ARKit／ARCore 每幀估出房間的亮度與色溫，角色就跟著房間明暗變化（暗房間不會亮得很突兀、黃光燈下偏暖）。
    ///   ・賽璐珞材質：全域 _SD_EnvTint（Toon.shader 最後乘上）。
    ///   ・一般受光材質：主方向光的強度與顏色、環境光。
    /// 變化會慢慢過渡（不閃爍）；亮度夾在 0.45～1.15 倍、色偏只取一半（不讓角色整個染色）。
    /// 手機不支援時維持原本的亮度。設定選單可以關掉。
    /// </summary>
    public class ArLighting : MonoBehaviour
    {
        ARCameraManager cams;
        float targetK = 1f, k = 1f;
        Color targetTint = Color.white, tint = Color.white;
        Light sun; float sunBase = -1f, nextFind; Color ambientBase; bool ambientSaved;
        public static bool On = true;
        /// <summary>目前的亮度倍率與色偏（除錯／介面用）</summary>
        public static float Brightness { get; private set; } = 1f;

        public static ArLighting Attach(ARCameraManager manager)
        {
            var l = manager.gameObject.AddComponent<ArLighting>();
            l.cams = manager;
            try { On = PlayerPrefs.GetInt("SD_ArLight", 1) == 1; } catch { }
            manager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor;
            manager.frameReceived += l.OnFrame;
            return l;
        }
        public static void SetOn(bool v) { On = v; try { PlayerPrefs.SetInt("SD_ArLight", v ? 1 : 0); PlayerPrefs.Save(); } catch { } }

        void OnDestroy() { if (cams != null) cams.frameReceived -= OnFrame; Shader.SetGlobalColor("_SD_EnvTint", Color.white); }

        void OnFrame(ARCameraFrameEventArgs e)
        {
            var le = e.lightEstimation;
            float? b = null;
            if (le.averageBrightness.HasValue) b = le.averageBrightness.Value * 2f;                  // ARCore：0～1，0.5 左右＝一般室內
            else if (le.averageIntensityInLumens.HasValue) b = le.averageIntensityInLumens.Value / 1000f;   // ARKit：1000 流明＝中性
            if (b.HasValue) targetK = Mathf.Clamp(b.Value, 0.45f, 1.15f);
            Color c = Color.white; bool hasC = false;
            if (le.colorCorrection.HasValue) { c = le.colorCorrection.Value; hasC = true; }
            else if (le.averageColorTemperature.HasValue) { c = Mathf.CorrelatedColorTemperatureToRGB(le.averageColorTemperature.Value); hasC = true; }
            if (hasC)
            {
                float m = Mathf.Max(0.001f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                c = new Color(c.r / m, c.g / m, c.b / m);
                targetTint = Color.Lerp(Color.white, c, 0.5f);
            }
        }

        void Update()
        {
            float tk = On ? targetK : 1f; var tt = On ? targetTint : Color.white;
            float s = 1f - Mathf.Exp(-Time.deltaTime * 2f);   // 約 0.5 秒過渡
            k = Mathf.Lerp(k, tk, s); tint = Color.Lerp(tint, tt, s);
            Brightness = k;
            Shader.SetGlobalColor("_SD_EnvTint", new Color(tint.r * k, tint.g * k, tint.b * k, 1f));
            // 一般受光材質：方向光與環境光
            if (sun == null && Time.time >= nextFind)   // 方向光由角色第一次建立時產生：每 2 秒找一次，找到就不再找
            {
                nextFind = Time.time + 2f;
                foreach (var l in FindObjectsOfType<Light>()) if (l.type == LightType.Directional && l.enabled) { sun = l; sunBase = l.intensity; break; }
            }
            if (sun != null) { sun.intensity = sunBase * k; sun.color = tint; }
            if (!ambientSaved) { ambientBase = RenderSettings.ambientLight.maxColorComponent < 0.35f ? new Color(0.45f, 0.45f, 0.5f) : RenderSettings.ambientLight; ambientSaved = true; }   // 和角色的預設環境光一致
            RenderSettings.ambientLight = new Color(ambientBase.r * tint.r * k, ambientBase.g * tint.g * k, ambientBase.b * tint.b * k, 1f);
        }
    }
}
