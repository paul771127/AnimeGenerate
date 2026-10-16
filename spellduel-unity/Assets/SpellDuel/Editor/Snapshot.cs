using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpellDuel.EditorTools
{
    /// <summary>
    /// 雲端截圖（GameCI buildMethod：SpellDuel.EditorTools.Snapshot.Render）：
    /// 把五個職業的角色（遠端連線時隊友／對手的虛擬角色）和召喚物各拍一張，存到 snapshots/ 給 CI 上傳。
    /// </summary>
    public static class Snapshot
    {
        const int W = 640, H = 900;

        public static void Render()
        {
            Directory.CreateDirectory("snapshots");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("SnapCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.84f, 0.92f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 2f;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.6f, 0.5f) };

            foreach (var cls in Skills.ClassOrder)
            {
                var rig = CharacterRig.Create(cls);
                rig.transform.position = Vector3.zero;
                // 正面、斜側面
                Shot(cam, rig.gameObject, cls + "_front", new Vector3(0f, 1.1f, 3.6f), new Vector3(0f, 0.95f, 0f));
                Shot(cam, rig.gameObject, cls + "_side", new Vector3(2.6f, 1.3f, 2.5f), new Vector3(0f, 0.95f, 0f));
                Object.DestroyImmediate(rig.gameObject);
            }
            // 召喚物
            string[] pets = { "falcon", "fire", "golem", "clone" };
            foreach (var k in pets)
            {
                var pr = PetRig.Create(k, new Color(0.9f, 0.5f, 1f));
                pr.SetPose(new Vector3(0f, k == "golem" ? 0.6f : 0.9f, 0f), Quaternion.Euler(0f, 180f, 0f), 0.3f, 1f);
                Shot(cam, pr.gameObject, "pet_" + k, new Vector3(0f, 1.2f, 2.6f), new Vector3(0f, 0.8f, 0f));
                Object.DestroyImmediate(pr.gameObject);
            }
            // 全職業合照
            var row = new GameObject("Row");
            for (int i = 0; i < Skills.ClassOrder.Length; i++)
            {
                var rig = CharacterRig.Create(Skills.ClassOrder[i], row.transform);
                rig.transform.localPosition = new Vector3((i - 2) * 0.95f, 0f, 0f);
            }
            Shot(cam, row, "all_classes", new Vector3(0f, 1.4f, 7.5f), new Vector3(0f, 0.95f, 0f), 1600, 800);
            Debug.Log("[SpellDuel] snapshots saved to " + Path.GetFullPath("snapshots"));
        }

        static void Shot(Camera cam, GameObject subject, string name, Vector3 camPos, Vector3 look, int w = W, int h = H)
        {
            cam.transform.position = camPos;
            cam.transform.LookAt(look);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine("snapshots", name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
