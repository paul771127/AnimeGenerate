using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色畫面「裝備」分頁的小預覽：在場景很遠的地方（看不到的地下）放一個自己職業的角色，
    /// 用一台只畫到 RenderTexture 的相機拍它，慢慢轉一圈給你看。
    /// 只在畫面有畫出來的時候才拍（半秒沒被畫到就關掉相機），不影響戰鬥效能。
    /// </summary>
    public class GearPreview : MonoBehaviour
    {
        static GearPreview inst;
        static readonly Vector3 Spot = new Vector3(0f, -1000f, 0f);

        Camera cam;
        RenderTexture rt;
        CharacterRig rig;
        string cls, adv;
        float yaw = 200f, lastDraw = -10f;

        /// <summary>畫在 r 裡面（IMGUI）；裝備變了會自動換</summary>
        public static void Draw(Rect r, CharacterProfile p)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!inst) { var go = new GameObject("Gear Preview"); DontDestroyOnLoad(go); inst = go.AddComponent<GearPreview>(); }
            inst.Show(r, p);
        }

        void Show(Rect r, CharacterProfile p)
        {
            lastDraw = Time.unscaledTime;
            EnsureCamera();
            if (!rig || cls != p.cls || adv != p.adv)
            {
                if (rig) Destroy(rig.gameObject);
                cls = p.cls; adv = p.adv;
                rig = CharacterRig.Create(cls, adv, transform);
                rig.InCombat = false;
            }
            if (!rig.gameObject.activeSelf) rig.gameObject.SetActive(true);
            DemoPoses();
            rig.SetGear(GearLook.Of(p.equipped));
            rig.SetPose(Spot, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            if (!cam.enabled) cam.enabled = true;
            GUI.DrawTexture(r, rt, ScaleMode.ScaleToFit, true);
        }

        /// <summary>預覽輪流展示：站姿 → 戰鬥架勢 → 蓄力 → 出招 → 防禦（每段約 2 秒；蹲、跳在戰鬥中照真人的手機）</summary>
        int demoStep = -1;
        void DemoPoses()
        {
            float demoT = Time.unscaledTime;   // 一幀可能畫好幾次：用時間算，不要累加
            int step = Mathf.FloorToInt(demoT / 2.2f) % 5;
            if (step == demoStep) { if (step == 2) rig.Charge = Mathf.Clamp01((demoT % 2.2f) / 2f); return; }
            demoStep = step;
            rig.InCombat = step != 0; rig.Charge = 0f; rig.Guarding = step == 4;
            if (step == 3) rig.PlayAttack(AttackKind(cls), 0.15f);
        }
        static string AttackKind(string c) => c == "swordsman" ? "slash" : c == "assassin" ? "stab" : c == "archer" ? "shoot" : c == Skills.Novice ? "novice" : "magic";

        void EnsureCamera()
        {
            if (cam) return;
            rt = new RenderTexture(384, 576, 16) { name = "GearPreview", antiAliasing = 2 };
            var go = new GameObject("Gear Preview Camera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 12f;
            cam.depth = -50f;
            cam.transform.position = Spot + new Vector3(0f, 1.05f, 4.4f);
            cam.transform.rotation = Quaternion.LookRotation(Spot + new Vector3(0f, 0.85f, 0f) - cam.transform.position, Vector3.up);
        }

        void Update()
        {
            if (!cam) return;
            bool on = Time.unscaledTime - lastDraw < 0.5f;
            if (cam.enabled != on) cam.enabled = on;
            if (rig && rig.gameObject.activeSelf != on) rig.gameObject.SetActive(on);
            if (on) yaw += Time.unscaledDeltaTime * 20f;   // 慢慢轉
        }

        void OnDestroy() { if (rt) rt.Release(); }
    }
}
