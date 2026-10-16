using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>Local presentation of existing events. Never schedules hits or changes a Battle object.</summary>
    public static class AdvancedFx
    {
        static Material glow, lit;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static AdvancedFxMotion Begin(string name, Vector3 at, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(at, WorldFrame.RotFromWorld(Quaternion.identity));
            var fx = go.AddComponent<AdvancedFxMotion>(); fx.life = lifetime;
            Object.Destroy(go, lifetime + 0.1f);
            return fx;
        }
        internal static Material GlowMaterial
        {
            get
            {
                if (glow == null) glow = new Material(Shader.Find("Sprites/Default")) { name = "Advanced FX glow", color = Color.white };
                return glow;
            }
        }
        static Transform Piece(AdvancedFxMotion fx, PrimitiveType type, Vector3 pos, Vector3 size, Color c, bool solid = false, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Object.Destroy(collider); }
            if (type == PrimitiveType.Cube && solid)
            {
                var mf = go.GetComponent<MeshFilter>(); mf.sharedMesh = CharacterRig.SmoothCube(mf.sharedMesh);
            }
            go.transform.SetParent(fx.transform, false); go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler); go.transform.localScale = size;
            if (solid && lit == null) lit = new Material(ToonStyle.LitShader()) { name = "Advanced FX cel", color = Color.white };
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = solid ? lit : GlowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var block = new MaterialPropertyBlock(); block.SetColor(ColorId, c); r.SetPropertyBlock(block);
            fx.Add(go.transform, r, c, !solid);
            return go.transform;
        }
        internal static LineRenderer Ring(Transform parent, float radius, Color c, float y = 0.03f)
        {
            var go = new GameObject("Rune circle"); go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * y;
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = GlowMaterial; line.useWorldSpace = false; line.loop = true;
            line.positionCount = 48; line.widthMultiplier = 0.025f;
            line.startColor = line.endColor = c;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            return line;
        }
        static void AnimatedRing(AdvancedFxMotion fx, float radius, Color c, float y, float speed)
        {
            var line = Ring(fx.transform, radius, c, y);
            fx.Add(line.transform, line, c, true, speed);
        }
        static ParticleSystem Puff(Transform parent, Color c, int count, float size, float lifetime, bool smoke)
        {
            var ps = SpellFx.NewPS(parent, smoke ? "Smoke" : "Embers", smoke ? SpellFx.BlendMat(SpellFx.TexSoft) : SpellFx.AddMat(SpellFx.TexSoft), count, false);
            var main = ps.main; main.loop = false; main.startLifetime = lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.9f); main.startColor = c;
            SpellFx.Sphere(ps, 0.18f); SpellFx.FadeInOut(ps);
            SpellFx.SizeCurve(ps, smoke ? SpellFx.Grow : SpellFx.Shrink);
            if (smoke) SpellFx.Noise(ps, 0.25f, 1.2f);
            ps.Play(); ps.Emit(count);
            return ps;
        }

        public static void Area(string id, Vector3 at, float radius, Color c)
        {
            switch (id)
            {
                case "sanction":
                {
                    var fx = Begin("Sanction hammer", at, 0.9f);
                    var gold = new Color(1f, 0.78f, 0.18f, 0.8f);
                    AnimatedRing(fx, radius, gold, 0.04f, 0.9f);
                    AnimatedRing(fx, radius * 0.7f, gold, 0.08f, 1.4f);
                    var head = Piece(fx, PrimitiveType.Cube, new Vector3(0f, 1.25f, 0f), new Vector3(0.85f, 0.35f, 0.40f), gold);
                    fx.Fall(head, 0.95f);
                    var handle = Piece(fx, PrimitiveType.Cylinder, new Vector3(0f, 1.75f, 0f), new Vector3(0.09f, 0.45f, 0.09f), gold);
                    fx.Fall(handle, 0.95f);
                    Puff(fx.transform, new Color(1f, 0.90f, 0.5f), 18, 0.12f, 0.7f, false);
                    break;
                }
                case "whirlwind":
                {
                    var fx = Begin("Crimson blade cyclone", at, 0.85f);
                    for (int i = 0; i < 3; i++)
                    {
                        var line = new GameObject("Blade arc").AddComponent<LineRenderer>();
                        line.transform.SetParent(fx.transform, false); line.useWorldSpace = false;
                        line.sharedMaterial = GlowMaterial; line.positionCount = 26;
                        line.widthMultiplier = 0.08f; line.startColor = new Color(1f, 0.18f, 0.14f, 0.85f); line.endColor = new Color(1f, 0.5f, 0.2f, 0.05f);
                        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
                        for (int j = 0; j < 26; j++)
                        {
                            float a = j * 4.7f / 25f + i * 2.1f;
                            line.SetPosition(j, new Vector3(Mathf.Cos(a) * radius, 0.20f + i * 0.24f + j * 0.005f, Mathf.Sin(a) * radius));
                        }
                        fx.Add(line.transform, line, Color.white, true, 0.12f, 440f + i * 80f);
                    }
                    break;
                }
                case "frostfield":
                {
                    var fx = Begin("Frost rune and crystals", at, 1.4f);
                    var ice = new Color(0.30f, 0.82f, 1f);
                    AnimatedRing(fx, radius, new Color(0.6f, 0.95f, 1f, 0.7f), 0.025f, 0.1f);
                    // A six-spoked snowflake on the ground; eight solid cel crystals.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f;
                        Piece(fx, PrimitiveType.Cube, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.035f, radius * 0.36f),
                            new Vector3(0.025f, 0.02f, radius * 0.72f), new Color(0.65f, 1f, 1f, 0.7f), false, new Vector3(0f, a, 0f));
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f, h = 0.48f + (i % 3) * 0.17f;
                        var p = Piece(fx, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * radius * 0.72f, h * 0.5f, Mathf.Sin(a) * radius * 0.72f),
                            new Vector3(0.16f, h, 0.16f), ice, true, new Vector3(0f, i * 45f, 8f));
                        fx.Grow(p);
                    }
                    Puff(fx.transform, new Color(0.45f, 0.88f, 1f, 0.38f), 20, 0.35f, 1.1f, true);
                    break;
                }
                case "detonate":
                {
                    var fx = Begin("Elemental detonation", at, 1.1f);
                    AnimatedRing(fx, radius, new Color(1f, 0.50f, 0.08f, 0.8f), 0.05f, 0.8f);
                    Piece(fx, PrimitiveType.Sphere, Vector3.up * 0.32f, Vector3.one * Mathf.Min(radius, 0.9f), new Color(1f, 0.75f, 0.2f, 0.65f));
                    Puff(fx.transform, new Color(1f, 0.40f, 0.05f), 32, 0.30f, 0.85f, false);
                    Puff(fx.transform, new Color(0.48f, 0.26f, 0.30f, 0.40f), 16, 0.50f, 0.95f, true);
                    break;
                }
                default: SpellFx.DispelRing(at, radius, c); break;
            }
        }

        public static void Summon(string kind, Vector3 at, Color c)
        {
            var fx = Begin("Summoning sigil " + kind, at, 0.85f);
            float y = kind == "golem" ? -0.57f : kind == "clone" ? -0.86f : -0.16f;
            AnimatedRing(fx, kind == "golem" ? 0.58f : 0.38f, c, y, 0.18f);
            AnimatedRing(fx, kind == "golem" ? 0.43f : 0.27f, Color.Lerp(c, Color.white, 0.5f), y + 0.035f, -0.12f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Piece(fx, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.33f, y + 0.03f, Mathf.Sin(a) * 0.33f),
                    new Vector3(0.07f, 0.018f, 0.10f), c, false, new Vector3(0f, -i * 60f, 0f));
            }
            Puff(fx.transform, c, 12, 0.09f, 0.7f, false);
        }
        public static void Dismiss(string kind, Vector3 at)
        {
            var fx = Begin("Pet dispersal " + kind, at, 1.2f);
            var c = kind == "fire" ? new Color(1f, 0.5f, 0.2f, 0.42f) : kind == "clone" ? new Color(0.55f, 0.65f, 0.85f, 0.38f) : new Color(0.7f, 0.78f, 0.83f, 0.38f);
            Puff(fx.transform, c, 18, kind == "golem" ? 0.48f : 0.27f, 1f, true);
        }
        public static void FogCast(Vector3 at)
        {
            var fx = Begin("Poison fog bloom", at, 1.1f);
            Puff(fx.transform, new Color(0.38f, 0.85f, 0.24f, 0.30f), 18, 0.45f, 0.9f, true);
        }
    }

    internal class AdvancedFxMotion : MonoBehaviour
    {
        sealed class Part
        {
            public Transform t; public Renderer renderer; public Color color; public bool fade, grow;
            public Vector3 scale, pos; public Quaternion rotation; public float expand, spin, fall;
        }
        readonly List<Part> parts = new List<Part>();
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        public float life = 1f;
        float age;
        void Awake() { block = new MaterialPropertyBlock(); }
        public void Add(Transform t, Renderer renderer, Color c, bool fade, float expand = 0f, float spin = 0f)
        {
            parts.Add(new Part { t = t, renderer = renderer, color = c, fade = fade, scale = t.localScale, pos = t.localPosition, rotation = t.localRotation, expand = expand, spin = spin });
        }
        public void Fall(Transform t, float distance) { foreach (var p in parts) if (p.t == t) p.fall = distance; }
        public void Grow(Transform t) { foreach (var p in parts) if (p.t == t) p.grow = true; }
        void Update()
        {
            age += Time.deltaTime; float t = Mathf.Clamp01(age / life);
            foreach (var p in parts)
            {
                float s = p.grow ? Mathf.Clamp01(t / 0.20f) * (1f - Mathf.Clamp01((t - 0.78f) / 0.22f)) : 1f + p.expand * t;
                p.t.localScale = p.scale * s;
                // Crystals grow from the ground rather than through it.
                p.t.localPosition = p.pos - Vector3.up * p.fall * Mathf.Clamp01(t / 0.30f);
                if (p.grow) p.t.localPosition = new Vector3(p.pos.x, p.pos.y * s, p.pos.z);
                p.t.localRotation = p.rotation * Quaternion.Euler(0f, age * p.spin, 0f);
                if (p.fade)
                {
                    var c = p.color; c.a *= 1f - t;
                    block.SetColor(ColorId, c); p.renderer.SetPropertyBlock(block);
                }
            }
        }
    }

    /// <summary>Owned by burnGo; removed with its authoritative BurnZone, never a second timer.</summary>
    internal class PoisonFogView : MonoBehaviour
    {
        ParticleSystem mist;
        ParticleSystemRenderer mistRenderer;
        LineRenderer edge;
        MaterialPropertyBlock block;
        static readonly int TintId = Shader.PropertyToID("_TintColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        void Awake() { block = new MaterialPropertyBlock(); }
        public static GameObject Create(float radius)
        {
            var view = new GameObject("Poison fog ground").AddComponent<PoisonFogView>();
            view.transform.rotation = WorldFrame.RotFromWorld(Quaternion.identity);
            view.edge = AdvancedFx.Ring(view.transform, radius, new Color(0.5f, 0.95f, 0.27f, 0.28f));
            view.mist = SpellFx.NewPS(view.transform, "Rolling poison mist", SpellFx.BlendMat(SpellFx.TexSoft), 48, false);
            view.mistRenderer = view.mist.GetComponent<ParticleSystemRenderer>();
            view.mist.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var m = view.mist.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.65f, 0.18f, 0.24f), new Color(0.6f, 0.93f, 0.32f, 0.30f));
            SpellFx.Disc(view.mist, Mathf.Max(0.05f, radius - 0.35f));
            SpellFx.Noise(view.mist, 0.12f, 0.7f); SpellFx.FadeInOut(view.mist); SpellFx.SizeCurve(view.mist, SpellFx.Grow);
            SpellFx.Rate(view.mist, 24f); view.mist.Play(); view.mist.Emit(12);
            return view.gameObject;
        }
        public void SetFade(float fade)
        {
            fade = Mathf.Clamp01(fade);
            var c = new Color(1f, 1f, 1f, fade);
            block.SetColor(TintId, c); block.SetColor(ColorId, c); mistRenderer.SetPropertyBlock(block);
            var edgeColor = new Color(0.5f, 0.95f, 0.27f, 0.28f * fade);
            edge.startColor = edge.endColor = edgeColor;
            SpellFx.Rate(mist, 24f * fade);
        }
    }
}
