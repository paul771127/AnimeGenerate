using UnityEngine;

namespace SpellDuel
{
    /// <summary>故事關卡的場地機關外觀：草叢、水窪（地上的區域）；爆炸桶、石柱在 HazardArt.Rooms.cs 的 RoomProp</summary>
    public static partial class HazardArt
    {
        public static ArenaZoneArt ArenaZone(int kind, float radius)
        {
            var v = new GameObject(kind == Battle.ZoneGrass ? "Arena grass" : "Arena puddle").AddComponent<ArenaZoneArt>();
            v.Kind = kind;
            var rng = new System.Random(kind * 7919 + Mathf.RoundToInt(radius * 1000f));
            float R() => (float)rng.NextDouble();
            if (kind == Battle.ZoneGrass)
            {
                // 一叢一叢的高草（每叢 3 片葉子），燒完換成焦黑的地面
                v.Tufts = new GameObject("Tufts").transform; v.Tufts.SetParent(v.transform, false);
                for (int i = 0; i < 30; i++)
                {
                    float a = R() * Mathf.PI * 2f, d = Mathf.Sqrt(R()) * radius * 0.92f;
                    var c = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                    var g = new Color(0.25f + 0.15f * R(), 0.55f + 0.25f * R(), 0.18f);
                    for (int b = 0; b < 3; b++)
                    {
                        float ba = R() * Mathf.PI * 2f, lean = 0.05f + 0.08f * R(), h = 0.22f + 0.2f * R();
                        var tip = c + new Vector3(Mathf.Cos(ba) * lean, h, Mathf.Sin(ba) * lean);
                        Ribbon(v.Tufts, "Blade", new[] { c + Vector3.up * 0.01f, Vector3.Lerp(c, tip, 0.55f) + Vector3.up * 0.02f, tip }, 0.03f, g);
                    }
                }
                Ring(v.transform, "Grass edge", radius, new Color(0.35f, 0.6f, 0.2f, 0.35f));
                v.Scorch = Part(v.transform, "Scorch", ArtKit.FlatDisc(radius, 28, 0.012f, "Scorched ground"), NatureGlow);
                var blk = new MaterialPropertyBlock(); blk.SetColor("_Color", new Color(0.08f, 0.05f, 0.04f, 0.8f)); v.Scorch.GetComponent<Renderer>().SetPropertyBlock(blk);
                v.Scorch.SetActive(false);
                v.Fire = SpellFx.NewPS(v.transform, "Grass fire", SpellFx.AddMat(SpellFx.TexSoft), 90, false);
                var m = v.Fire.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.8f, 0.25f)); m.gravityModifier = -0.25f;
                var sh = v.Fire.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(radius * 1.6f, 0.05f, radius * 1.6f);
                var em = v.Fire.emission; em.rateOverTime = 70f;
                SpellFx.FadeOut(v.Fire);
            }
            else
            {
                // 水窪：深色的水面＋淡淡的反光圈；導電時冒出閃電，結凍時蓋一層冰
                v.Water = Part(v.transform, "Water", ArtKit.FlatDisc(radius, 32, 0.01f, "Puddle water"), NatureGlow);
                var blk = new MaterialPropertyBlock(); blk.SetColor("_Color", new Color(0.15f, 0.3f, 0.45f, 0.65f)); v.Water.GetComponent<Renderer>().SetPropertyBlock(blk);
                Ring(v.transform, "Puddle shine", radius * 0.7f, new Color(0.7f, 0.9f, 1f, 0.35f));
                Ring(v.transform, "Puddle edge", radius, new Color(0.45f, 0.65f, 0.8f, 0.5f));
                v.Ice = Part(v.transform, "Ice", ArtKit.FlatDisc(radius * 1.02f, 32, 0.02f, "Puddle ice"), NatureGlow);
                var ib = new MaterialPropertyBlock(); ib.SetColor("_Color", new Color(0.8f, 0.95f, 1f, 0.8f)); v.Ice.GetComponent<Renderer>().SetPropertyBlock(ib);
                v.Ice.SetActive(false);
                v.Sparks = new GameObject("Sparks").transform; v.Sparks.SetParent(v.transform, false);
                for (int i = 0; i < 5; i++) Ribbon(v.Sparks, "Arc", new Vector3[6], 0.03f, new Color(1f, 0.95f, 0.45f));
                v.Sparks.gameObject.SetActive(false);
            }
            v.Lines = v.GetComponentsInChildren<LineRenderer>(true);
            v.Radius = radius;
            return v;
        }
    }

    public sealed class ArenaZoneArt : MonoBehaviour
    {
        public int Kind, State = -1; public float Radius;
        public Transform Tufts, Sparks; public GameObject Scorch, Water, Ice; public ParticleSystem Fire; public LineRenderer[] Lines;
        public void Tick(int state, float clock)
        {
            if (state != State)
            {
                State = state;
                if (Kind == Battle.ZoneGrass)
                {
                    if (state == 1) Fire.Play(); else Fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    Tufts.gameObject.SetActive(state != 2);
                    Scorch.SetActive(state == 2);
                    var c = state == 1 ? new Color(0.35f, 0.18f, 0.08f) : new Color(0.3f, 0.6f, 0.2f);
                    if (state == 1) foreach (var l in Tufts.GetComponentsInChildren<LineRenderer>()) l.startColor = l.endColor = c;
                }
                else
                {
                    Ice.SetActive(state == 2);
                    Sparks.gameObject.SetActive(state == 1);
                }
            }
            if (Kind == Battle.ZonePuddle && state == 1)
            {
                // 閃電：每幀重新折一次
                foreach (Transform t in Sparks)
                {
                    var l = t.GetComponent<LineRenderer>(); if (!l) continue;
                    float a = Random.value * Mathf.PI * 2f;
                    var from = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius * Random.Range(0.1f, 0.9f);
                    var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    for (int i = 0; i < l.positionCount; i++)
                    {
                        var p = from + dir * (i * Radius * 0.18f) + Quaternion.Euler(0f, 90f, 0f) * dir * Random.Range(-0.08f, 0.08f);
                        p.y = 0.03f + Random.Range(0f, 0.18f);
                        l.SetPosition(i, p);
                    }
                }
            }
        }
        void OnDestroy() { foreach (var f in GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh) { if (Application.isPlaying) Destroy(f.sharedMesh); else DestroyImmediate(f.sharedMesh); } }
    }
}
