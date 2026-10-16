using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpellDuel.EditorTools
{
    /// <summary>
    /// 雲端截圖（GameCI buildMethod：SpellDuel.EditorTools.Snapshot.Render）：
    /// Render five base classes, ten advanced classes, face/expression studies and pets.
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
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.66f);
            var fronts = new List<string>();

            foreach (var cls in Skills.ClassOrder)
            {
                var rig = CharacterRig.Create(cls);
                rig.transform.position = Vector3.zero;
                CharacterShots(cam, rig, cls);
                fronts.Add(cls + "_front");
                Object.DestroyImmediate(rig.gameObject);
            }
            ContactSheet(fronts, "all_classes", 5);
            fronts.Clear();
            foreach (var cls in Skills.ClassOrder)
                foreach (var adv in Advanced.Of(cls))
                {
                    var rig = CharacterRig.Create(cls, adv.id);
                    CharacterShots(cam, rig, adv.id);
                    fronts.Add(adv.id + "_front");
                    Object.DestroyImmediate(rig.gameObject);
                }
            ContactSheet(fronts, "all_advanced", 5);
            // 召喚物
            string[] pets = { "falcon", "fire", "golem", "clone" };
            foreach (var k in pets)
            {
                try
                {
                    var pr = PetRig.Create(k, new Color(0.9f, 0.5f, 1f));
                    // 編輯模式不會呼叫 Awake／Update：直接擺位置就好
                    pr.transform.SetPositionAndRotation(new Vector3(0f, k == "golem" ? 0.6f : 0.9f, 0f), Quaternion.Euler(0f, 180f, 0f));
                    Shot(cam, pr.gameObject, "pet_" + k, new Vector3(0f, 1.2f, 2.6f), new Vector3(0f, 0.8f, 0f));
                    Object.DestroyImmediate(pr.gameObject);
                }
                catch (System.Exception e) { Debug.LogWarning($"[SpellDuel] pet {k} snapshot failed: {e.Message}"); }
            }
            Debug.Log("[SpellDuel] snapshots saved to " + Path.GetFullPath("snapshots"));
        }

        static void CharacterShots(Camera cam, CharacterRig rig, string id)
        {
            int triangles = 0;
            foreach (var mf in rig.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) triangles += mf.sharedMesh.triangles.Length / 3;
            Debug.Log($"[SpellDuel] {id}: {triangles} triangles");
            if (triangles > 10000) throw new System.InvalidOperationException(id + " exceeds the 10000 triangle budget");
            Shot(cam, rig.gameObject, id + "_front", new Vector3(0f, 1.2f, 4.9f), new Vector3(0f, 1.08f, 0f));
            Shot(cam, rig.gameObject, id + "_side", new Vector3(3.3f, 1.3f, 3.6f), new Vector3(0f, 1.08f, 0f));
            Shot(cam, rig.gameObject, id + "_face", new Vector3(0.24f, 1.58f, 0.80f), new Vector3(0f, 1.56f, 0f), 640, 640);
            // Edit mode has no Update; invoke only the rig's existing visual animation step.
            var animate = typeof(CharacterRig).GetMethod("Animate", BindingFlags.Instance | BindingFlags.NonPublic);
            rig.Charge = 0.9f;
            for (int i = 0; i < 15; i++) animate.Invoke(rig, new object[] { 1f / 30f });
            rig.PlayCast(); animate.Invoke(rig, new object[] { 0.10f });
            Shot(cam, rig.gameObject, id + "_cast", new Vector3(2.6f, 1.35f, 4.15f), new Vector3(0f, 1.08f, 0f));
            rig.Charge = 0f; rig.PlayHit(); animate.Invoke(rig, new object[] { 0.015f });
            Shot(cam, rig.gameObject, id + "_hurt", new Vector3(0.23f, 1.58f, 0.80f), new Vector3(0f, 1.56f, 0f), 640, 640);
        }

        static void ContactSheet(List<string> images, string name, int columns)
        {
            const int tileW = 320, tileH = 450;
            int rows = (images.Count + columns - 1) / columns;
            var sheet = new Texture2D(columns * tileW, rows * tileH, TextureFormat.RGB24, false);
            for (int i = 0; i < images.Count; i++)
            {
                var src = new Texture2D(2, 2);
                src.LoadImage(File.ReadAllBytes(Path.Combine("snapshots", images[i] + ".png")));
                var pixels = new Color32[tileW * tileH];
                for (int y = 0; y < tileH; y++)
                    for (int x = 0; x < tileW; x++) pixels[y * tileW + x] = src.GetPixelBilinear((x + 0.5f) / tileW, (y + 0.5f) / tileH);
                sheet.SetPixels32((i % columns) * tileW, (rows - 1 - i / columns) * tileH, tileW, tileH, pixels);
                Object.DestroyImmediate(src);
            }
            sheet.Apply(); File.WriteAllBytes(Path.Combine("snapshots", name + ".png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
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
