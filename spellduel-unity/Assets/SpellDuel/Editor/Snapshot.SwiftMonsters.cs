#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        [Serializable] public class SwiftAuditRow
        {
            public int id, baseKind, meshTriangles, maximumFxTriangles, totalTriangles, colourCount, eyeCount;
            public bool cached, pivotsUnchanged, hit, charge, death, movingTrail, stoppedTrail, deadTrail;
        }
        [Serializable] public class SwiftAuditReport { public string unity; public SwiftAuditRow[] enemies; }
        static void SwiftTick(MinionRig rig, float dt)
        {
            typeof(MinionRig).GetMethod("Animate", RareFlags).Invoke(rig, new object[] { dt });
            var look = rig.GetComponent<MinionLook>();
            typeof(MinionLook).GetMethod("TickSwift", RareFlags).Invoke(look, new object[] { dt });
            typeof(MinionLook).GetMethod("ApplyLookColours", RareFlags).Invoke(look, null);
        }
        static MinionRig SwiftCreate(int id, bool before = false)
        {
            var v = MonsterVariants.Get(id); UnityEngine.Random.InitState(id);
            var r = MinionRig.Create(); r.SetVariant(v.baseKind); r.SetDress(v.theme);
            var d = before ? new VariantDef { id = v.id, tint = v.tint } : v;
            MinionLook.Apply(r, d); typeof(MinionRig).GetField("clock", RareFlags).SetValue(r, 0f); SwiftTick(r, 0);
            return r;
        }
        static string SwiftBones(MinionRig r)
        {
            var list = new List<string>();
            foreach (var f in typeof(MinionRig).GetFields(RareFlags))
                if (f.FieldType == typeof(Transform)) { var t = (Transform)f.GetValue(r); if (t) list.Add(f.Name + ":" + RarePath(t, r.transform) + ":" + t.localPosition + ":" + t.localRotation + ":" + t.localScale); }
            return string.Join("\n", list);
        }
        static SwiftAuditRow SwiftAudit(int id)
        {
            var r = SwiftCreate(id); var old = SwiftCreate(id, true); var clone = SwiftCreate(id);
            var a = new SwiftAuditRow { id = id, baseKind = MonsterVariants.Get(id).baseKind, pivotsUnchanged = SwiftBones(r) == SwiftBones(old), cached = true };
            var meshes = r.GetComponentsInChildren<MeshFilter>(); var others = clone.GetComponentsInChildren<MeshFilter>();
            a.cached = meshes.Length == others.Length;
            for (int i = 0; i < meshes.Length; i++)
            {
                for (int j = 0; j < meshes[i].sharedMesh.subMeshCount; j++) a.meshTriangles += (int)meshes[i].sharedMesh.GetIndexCount(j) / 3;
                if (i < others.Length) a.cached &= meshes[i].sharedMesh == others[i].sharedMesh;
            }
            foreach (var p in r.GetComponentsInChildren<ParticleSystem>()) a.maximumFxTriangles += p.main.maxParticles * 2;
            foreach (var l in r.GetComponentsInChildren<LineRenderer>()) a.maximumFxTriangles += (l.positionCount - 1) * 2;
            a.totalTriangles = a.meshTriangles + a.maximumFxTriangles + 2; // independent contact shadow
            a.colourCount = ((IList)RareField(r, "rockList")).Count; a.eyeCount = ((IList)RareField(r, "eyeList")).Count;
            var colourPart = (Renderer)((IList)RareField(r, "rockList"))[0]; var c = RareColour(colourPart);
            r.PlayHit(); SwiftTick(r, .01f); a.hit = RareColour(colourPart) != c; SwiftTick(r, 1);
            r.Windup(1); SwiftTick(r, 1); a.charge = RareColour(colourPart) != c;
            r.MoveSpeed = 3; for (int i = 0; i < 20; i++) { r.transform.position += Vector3.forward * .075f; SwiftTick(r, .025f); }
            var lines = r.GetComponentsInChildren<LineRenderer>(); a.movingTrail = lines.Length == 3;
            foreach (var l in lines) a.movingTrail &= l.enabled && Vector3.Distance(l.GetPosition(0), l.GetPosition(l.positionCount - 1)) > .2f;
            for (int i = 0; i < 30; i++) SwiftTick(r, .025f);
            a.stoppedTrail = true; foreach (var l in lines) a.stoppedTrail &= !l.enabled;
            r.SetDead(true); for (int i = 0; i < 40; i++) { r.transform.position += Vector3.forward * .05f; SwiftTick(r, .025f); }
            a.death = RareColour(colourPart) != c; a.deadTrail = true; foreach (var l in lines) a.deadTrail &= !l.enabled;
            Debug.Log("Swift audit " + JsonUtility.ToJson(a));
            UnityEngine.Object.DestroyImmediate(r.gameObject); UnityEngine.Object.DestroyImmediate(old.gameObject); UnityEngine.Object.DestroyImmediate(clone.gameObject);
            return a;
        }
        static Bounds SwiftBounds(MinionRig r)
        {
            var b = RareBounds(r);
            foreach (var l in r.GetComponentsInChildren<LineRenderer>()) if (l.enabled) b.Encapsulate(l.bounds);
            return b;
        }
        public static void RenderSwiftMonsters()
        {
            Directory.CreateDirectory("snapshots"); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); ToonStyle.Apply();
            var lamp = new GameObject("Swift key light").AddComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = 1.15f; lamp.transform.rotation = Quaternion.Euler(38, 155, 0);
            RenderSettings.ambientLight = new Color(.56f, .59f, .64f);
            var cam = new GameObject("Swift native camera").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.76f, .81f, .84f); cam.orthographic = true; cam.nearClipPlane = .02f;
            var rows = new List<SwiftAuditRow>();
            for (int id = 171; id <= 180; id++)
            {
                rows.Add(SwiftAudit(id));
                var before = SwiftCreate(id, true); var after = SwiftCreate(id); var bounds = RareBounds(after); bounds.Encapsulate(RareBounds(before));
                after.gameObject.SetActive(false); RareShot(cam, before.gameObject, "swift_" + id + "_before", bounds, new Vector3(.5f, .18f, 1)); before.gameObject.SetActive(false); after.gameObject.SetActive(true);
                foreach (var view in new[] { "front", "side", "rear45" })
                    RareShot(cam, after.gameObject, "swift_" + id + "_" + view, bounds, view == "front" ? new Vector3(0, .08f, 1) : view == "side" ? new Vector3(1, .08f, 0) : new Vector3(.7f, .18f, -1));
                after.MoveSpeed = 3;
                for (int i = 0; i < 24; i++) { after.transform.position += Vector3.forward * .075f; SwiftTick(after, .025f); }
                RareShot(cam, after.gameObject, "swift_" + id + "_run", SwiftBounds(after), new Vector3(.85f, .35f, .8f));
                after.Windup(1.5f); SwiftTick(after, 1.5f); after.Attack(); SwiftTick(after, .15f);
                RareShot(cam, after.gameObject, "swift_" + id + "_attack", SwiftBounds(after), new Vector3(.6f, .2f, .8f));
                UnityEngine.Object.DestroyImmediate(before.gameObject); UnityEngine.Object.DestroyImmediate(after.gameObject);
            }
            for (int chapter = 0; chapter < 5; chapter++)
            {
                var root = new GameObject("Swift scale comparison");
                for (int j = 0; j < 2; j++) { var r = SwiftCreate(171 + chapter * 2 + j); r.transform.SetParent(root.transform, false); r.transform.localPosition = new Vector3(j * 1.65f - 1.5f, 0, 0); }
                var normal = MinionRig.Create(); normal.SetVariant(MonsterVariants.Get(171 + chapter * 2).baseKind); normal.transform.SetParent(root.transform, false); normal.transform.localPosition = new Vector3(1.65f, 0, 0);
                var player = CharacterRig.Create("novice"); player.InCombat = false; player.transform.SetParent(root.transform, false); player.transform.localPosition = new Vector3(3.1f, 0, 0);
                cam.orthographicSize = 1.8f; Shot(cam, root, "swift_lineup_" + chapter, new Vector3(.8f, 2.3f, 10), new Vector3(.8f, .75f, 0), 1800, 900); UnityEngine.Object.DestroyImmediate(root);
            }
            File.WriteAllText("snapshots/swift_audit.json", JsonUtility.ToJson(new SwiftAuditReport { unity = Application.unityVersion, enemies = rows.ToArray() }, true));
            foreach (var a in rows)
            {
                int id = a.id;
            RareRequire(a.totalTriangles <= 2500, "swift triangle budget " + id + "=" + a.totalTriangles);
            RareRequire(a.cached && a.pivotsUnchanged, "swift cache / pivot mismatch " + id);
            RareRequire(a.hit && a.charge && a.death && a.colourCount > 0 && a.eyeCount > 0, "swift colour state " + id);
            RareRequire(a.movingTrail && a.stoppedTrail && a.deadTrail, "swift motion trail " + id);
            }
        }
    }
}
#endif
