using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>Presentation only. The root is the Battle pet's body center; no colliders or combat state.</summary>
    public class PetRig : MonoBehaviour
    {
        static readonly Dictionary<Color, Material> solid = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> ghost = new Dictionary<Color, Material>();
        readonly List<Renderer> stones = new List<Renderer>();
        readonly List<GameObject> cracks = new List<GameObject>();
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color Stone = new Color(0.52f, 0.68f, 0.74f);
        string kind;
        Transform wingL, wingR, flame, echoL, echoR;
        float previousHealth = 1f, hurtUntil, actionUntil, nextFairyLight;
        readonly List<Renderer> iceFaces=new List<Renderer>();
        Transform iceHandL,iceHandR;
        internal void ActionPulse(){actionUntil=Time.time+.28f;if(kind=="fairy"){SpellFx.FairySprinkle(transform.position);nextFairyLight=Time.time+3f;}}

        void Awake() { block = new MaterialPropertyBlock(); }

        public static PetRig Create(string kind, Color ownerColor)
        {
            var rig = new GameObject("Pet " + kind).AddComponent<PetRig>();
            rig.kind = kind;
            switch (kind)
            {
                case "falcon": rig.BuildFalcon(); break;
                case "fire": rig.BuildFire(); break;
                case "golem": rig.BuildGolem(); break;
                case "ice": rig.BuildIce(); break;
                case "fairy": rig.BuildFairy(); break;
                default: rig.BuildClone(ownerColor); break;
            }
            BlobShadow.Attach(rig.transform, kind == "golem" ? 0.45f : kind == "falcon" ? 0.22f : 0.3f, 0f, true);   // 腳下的接觸陰影（老鷹飛高時變淡）
            return rig;
        }

        static Mesh smallSphere,smallCapsule;
        static Mesh SmallBody(bool capsule)
        {
            if(capsule&&smallCapsule)return smallCapsule;if(!capsule&&smallSphere)return smallSphere;
            var m=new PMesh(1);m.Lathe(capsule?new[]{-1f,0f,-.85f,.35f,-.5f,.5f,.5f,.5f,.85f,.35f,1f,0f}:new[]{-.5f,0f,-.35f,.35f,0f,.5f,.35f,.35f,.5f,0f},10,null);
            var mesh=m.Build(capsule?"Small pet capsule":"Small pet sphere",true);var v=mesh.vertices;
            for(int i=0;i<v.Length;i++)v[i]=new Vector3(v[i].x,v[i].z,-v[i].y);mesh.vertices=v;mesh.RecalculateNormals();mesh.RecalculateBounds();
            if(capsule)smallCapsule=mesh;else smallSphere=mesh;return mesh;
        }

        static Material Mat(Color c, bool transparent)
        {
            var cache = transparent ? ghost : solid;
            if (cache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(transparent ? Shader.Find("Sprites/Default") : ToonStyle.LitShader())
                { color = c, name = transparent ? "Pet spectral" : "Pet cel" };
            cache[c] = m;
            return m;
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = pos;
            return t;
        }

        Renderer Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, bool transparent = false, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            // Disabled immediately; Destroy is deferred until the end of the frame.
            var collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            if((kind=="ice"||kind=="fairy")&&(type==PrimitiveType.Sphere||type==PrimitiveType.Capsule))go.GetComponent<MeshFilter>().sharedMesh=SmallBody(type==PrimitiveType.Capsule);
            if (type == PrimitiveType.Cube && !transparent)
            {
                var mf = go.GetComponent<MeshFilter>();
                mf.sharedMesh = CharacterRig.SmoothCube(mf.sharedMesh);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = Mat(color, transparent);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        void BuildFalcon()
        {
            var brown = new Color(0.64f, 0.33f, 0.14f);
            var cream = new Color(1f, 0.88f, 0.58f);
            Part(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(0.23f, 0.23f, 0.43f), brown);
            Part(PrimitiveType.Sphere, transform, new Vector3(0f, -0.04f, 0.09f), new Vector3(0.17f, 0.18f, 0.27f), cream);
            Part(PrimitiveType.Sphere, transform, new Vector3(0f, 0.07f, 0.23f), Vector3.one * 0.21f, cream);
            Part(PrimitiveType.Capsule, transform, new Vector3(0f, 0.02f, 0.36f), new Vector3(0.08f, 0.09f, 0.14f), new Color(1f, 0.65f, 0.08f), false, new Vector3(45f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                Part(PrimitiveType.Sphere, transform, new Vector3(s * 0.085f, 0.09f, 0.28f), new Vector3(0.035f, 0.045f, 0.035f), new Color(0.10f, 0.13f, 0.22f));
                var w = Pivot("Wing", transform, new Vector3(s * 0.09f, 0.04f, 0f));
                if (s < 0) wingL = w; else wingR = w;
                Part(PrimitiveType.Sphere, w, new Vector3(s * 0.18f, 0f, -0.03f), new Vector3(0.43f, 0.075f, 0.26f), brown);
                for (int i = 0; i < 4; i++)
                    Part(PrimitiveType.Capsule, w, new Vector3(s * (0.28f + i * 0.065f), 0f, -0.12f - i * 0.045f),
                        new Vector3(0.08f, 0.19f - i * 0.02f, 0.045f), i % 2 == 0 ? cream : brown, false, new Vector3(65f, 0f, s * -40f));
                Part(PrimitiveType.Capsule, transform, new Vector3(s * 0.06f, -0.01f, -0.29f), new Vector3(0.08f, 0.17f, 0.045f), cream, false, new Vector3(80f, s * 18f, 0f));
            }
        }

        void BuildFire()
        {
            flame = Pivot("Flame", transform, Vector3.zero);
            Part(PrimitiveType.Sphere, flame, Vector3.zero, new Vector3(0.33f, 0.40f, 0.30f), new Color(1f, 0.39f, 0.08f));
            Part(PrimitiveType.Sphere, flame, new Vector3(0f, -0.05f, 0.10f), new Vector3(0.23f, 0.27f, 0.17f), new Color(1f, 0.82f, 0.23f));
            for (int i = -1; i <= 1; i++)
                Part(PrimitiveType.Capsule, flame, new Vector3(i * 0.09f, 0.20f + (i == 0 ? 0.09f : 0f), 0f), new Vector3(0.12f, 0.14f, 0.10f),
                    new Color(1f, 0.61f, 0.12f), false, new Vector3(0f, 0f, i * -22f));
            for (int s = -1; s <= 1; s += 2)
            {
                Part(PrimitiveType.Sphere, flame, new Vector3(s * 0.065f, 0.02f, 0.175f), new Vector3(0.035f, 0.065f, 0.025f), new Color(0.17f, 0.12f, 0.24f));
                Part(PrimitiveType.Sphere, flame, new Vector3(s * 0.21f, -0.02f, 0f), new Vector3(0.13f, 0.10f, 0.10f), new Color(1f, 0.65f, 0.12f));
            }
            Part(PrimitiveType.Sphere, flame, new Vector3(0f, -0.075f, 0.18f), new Vector3(0.055f, 0.025f, 0.015f), new Color(0.63f, 0.20f, 0.15f));
            var tail = SpellFx.NewPS(transform, "Ember tail", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            tail.transform.localPosition = new Vector3(0f, -0.12f, -0.10f);
            var m = tail.main; m.startLifetime = 0.45f; m.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.14f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.04f), new Color(1f, 0.86f, 0.2f));
            m.startSpeed = 0.16f;
            SpellFx.FadeOut(tail); SpellFx.SizeCurve(tail, SpellFx.Shrink); SpellFx.Rate(tail, 22f); tail.Play();
        }

        /// <summary>冰靈：藍白色的冰晶精靈（三顆尖晶＋冷霧）</summary>
        void BuildIce()
        {
            flame=Pivot("Ice",transform,Vector3.zero);
            // Closed hexagonal crystal; tapered top/bottom, six lit facets instead of a ball.
            var m=new PMesh(1);m.Lathe(new[]{-.22f,0f,-.08f,.15f,.10f,.14f,.27f,0f},6,(z,j)=>j%2==0?new Color(.48f,.84f,1f):new Color(.78f,.96f,1f),1,1,30);
            var crystal=new GameObject("Hex crystal");crystal.transform.SetParent(flame,false);crystal.transform.localRotation=Quaternion.Euler(-90,0,0);
            crystal.AddComponent<MeshFilter>().sharedMesh=m.Build("Ice spirit crystal",true);
            var shell=crystal.AddComponent<MeshRenderer>();shell.sharedMaterial=Mat(Color.white,false);iceFaces.Add(shell);
            for(int side=-1;side<=1;side+=2)
            {
                Part(PrimitiveType.Sphere,flame,new Vector3(side*.055f,.015f,.135f),new Vector3(.035f,.050f,.016f),new Color(.13f,.24f,.48f));
                Part(PrimitiveType.Sphere,flame,new Vector3(side*.050f,.029f,.146f),Vector3.one*.012f,Color.white);
                var hand=Pivot("Ice hand",flame,new Vector3(side*.19f,-.015f,0));if(side<0)iceHandL=hand;else iceHandR=hand;
                var palm=Part(PrimitiveType.Capsule,hand,Vector3.zero,new Vector3(.07f,.055f,.065f),new Color(.45f,.84f,1f),false,new Vector3(0,0,side*35));iceFaces.Add(palm);
                for(int i=0;i<2;i++)Part(PrimitiveType.Sphere,hand,new Vector3((i-.5f)*.025f,.045f,.01f),new Vector3(.025f,.045f,.03f),new Color(.76f,.96f,1f));
            }
            Part(PrimitiveType.Sphere,flame,new Vector3(0,-.052f,.144f),new Vector3(.035f,.016f,.012f),new Color(.28f,.48f,.69f));
            SpellFx.Snow(transform);
        }

        void BuildFairy()
        {
            nextFairyLight=Time.time+3f;
            flame=Pivot("Fairy",transform,Vector3.zero);
            var pink=new Color(1f,.30f,.61f);var skin=new Color(1f,.88f,.77f);
            Part(PrimitiveType.Capsule,flame,new Vector3(0,-.015f,0),new Vector3(.095f,.105f,.075f),new Color(.86f,.32f,.72f));
            Part(PrimitiveType.Sphere,flame,new Vector3(0,.12f,0),new Vector3(.11f,.12f,.10f),skin);
            Part(PrimitiveType.Sphere,flame,new Vector3(0,.155f,-.015f),new Vector3(.12f,.075f,.11f),pink);
            for(int side=-1;side<=1;side+=2)
            {
                Part(PrimitiveType.Sphere,flame,new Vector3(side*.057f,.12f,-.01f),new Vector3(.055f,.11f,.06f),pink,false,new Vector3(0,0,side*12));
                Part(PrimitiveType.Sphere,flame,new Vector3(side*.023f,.122f,.050f),new Vector3(.015f,.025f,.010f),new Color(.24f,.17f,.40f));
                Part(PrimitiveType.Capsule,flame,new Vector3(side*.065f,.01f,.01f),new Vector3(.025f,.060f,.025f),skin,false,new Vector3(0,0,side*40));
                Part(PrimitiveType.Capsule,flame,new Vector3(side*.025f,-.15f,0),new Vector3(.022f,.065f,.023f),skin);
                // Four petal dress, not a glowing sphere.
                Part(PrimitiveType.Sphere,flame,new Vector3(side*.035f,-.075f,0),new Vector3(.075f,.095f,.075f),new Color(1f,.56f,.80f),false,new Vector3(0,0,side*22));
                var wing=Pivot("Butterfly wing",flame,new Vector3(side*.035f,.045f,-.045f));if(side<0)wingL=wing;else wingR=wing;
                Part(PrimitiveType.Sphere,wing,new Vector3(side*.085f,.05f,0),new Vector3(.19f,.15f,.009f),new Color(.88f,.72f,1f,.50f),true,new Vector3(0,0,side*24));
                Part(PrimitiveType.Sphere,wing,new Vector3(side*.068f,-.045f,0),new Vector3(.125f,.11f,.009f),new Color(1f,.73f,.90f,.45f),true,new Vector3(0,0,side*-18));
                SpellFx.Line(wing,"Wing vein",new[]{Vector3.zero,new Vector3(side*.09f,.07f,.008f),new Vector3(side*.16f,.09f,.008f)},false,.006f,new Color(1f,.88f,1f,.70f),false,true);
            }
            var dust=SpellFx.NewPS(transform,"Fairy light tail",SpellFx.BlendMat(SpellFx.TexStar),16,true);var d=dust.main;
            d.startLifetime=.7f;d.startSize=new ParticleSystem.MinMaxCurve(.015f,.030f);d.startSpeed=.04f;d.gravityModifier=.03f;
            d.startColor=new Color(1f,.72f,.92f,.65f);SpellFx.FadeOut(dust);SpellFx.Rate(dust,9);dust.Play();
        }

        Renderer Rock(Vector3 pos, Vector3 size)
        {
            var r = Part(PrimitiveType.Cube, transform, pos, size, Stone); stones.Add(r); return r;
        }
        void Crack(Vector3 a, Vector3 b)
        {
            var d = b - a;
            var r = Part(PrimitiveType.Cylinder, transform, (a + b) * 0.5f, new Vector3(0.013f, d.magnitude * 0.5f, 0.013f), new Color(1f, 0.24f, 0.09f, 0.9f), true);
            r.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            cracks.Add(r.gameObject); r.gameObject.SetActive(false);
        }
        void BuildGolem()
        {
            // +/-0.6m around the authoritative body center: feet stay on the ground.
            Rock(new Vector3(0f, 0.03f, 0f), new Vector3(0.48f, 0.44f, 0.30f));
            Rock(new Vector3(0f, 0.39f, 0f), new Vector3(0.32f, 0.35f, 0.29f));
            for (int s = -1; s <= 1; s += 2)
            {
                Rock(new Vector3(s * 0.32f, 0.11f, 0f), new Vector3(0.23f, 0.26f, 0.30f));
                Rock(new Vector3(s * 0.37f, -0.17f, 0.03f), new Vector3(0.21f, 0.29f, 0.27f));
                Rock(new Vector3(s * 0.14f, -0.34f, 0f), new Vector3(0.20f, 0.26f, 0.23f));
                Rock(new Vector3(s * 0.14f, -0.54f, 0.055f), new Vector3(0.23f, 0.12f, 0.34f));
                Part(PrimitiveType.Sphere, transform, new Vector3(s * 0.078f, 0.41f, 0.152f), new Vector3(0.07f, 0.035f, 0.025f), new Color(0.4f, 1f, 1f), true);
            }
            Part(PrimitiveType.Cube, transform, new Vector3(0f, 0.03f, 0.16f), new Vector3(0.11f, 0.13f, 0.03f), new Color(0.33f, 1f, 0.9f), true, new Vector3(0f, 0f, 45f));
            Crack(new Vector3(-0.18f, 0.22f, 0.158f), new Vector3(-0.08f, 0.10f, 0.158f));
            Crack(new Vector3(-0.08f, 0.10f, 0.158f), new Vector3(-0.16f, -0.11f, 0.158f));
            Crack(new Vector3(0.10f, 0.55f, 0.153f), new Vector3(0.03f, 0.47f, 0.153f));
            Crack(new Vector3(0.03f, 0.47f, 0.153f), new Vector3(0.12f, 0.33f, 0.153f));
            Crack(new Vector3(0.21f, 0.15f, 0.158f), new Vector3(0.12f, -0.15f, 0.158f));
            Crack(new Vector3(0.28f, -0.06f, 0.17f), new Vector3(0.42f, -0.26f, 0.17f));
        }

        void Ninja(Transform parent, Color c)
        {
            Part(PrimitiveType.Capsule, parent, new Vector3(0f, -0.10f, 0f), new Vector3(0.31f, 0.30f, 0.22f), c, true);
            Part(PrimitiveType.Sphere, parent, new Vector3(0f, 0.37f, 0f), Vector3.one * 0.30f, c, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(PrimitiveType.Capsule, parent, new Vector3(s * 0.22f, -0.08f, 0.02f), new Vector3(0.08f, 0.25f, 0.08f), c, true, new Vector3(-15f, 0f, s * 20f));
                Part(PrimitiveType.Capsule, parent, new Vector3(s * 0.10f, -0.56f, 0f), new Vector3(0.10f, 0.25f, 0.11f), c, true, new Vector3(0f, 0f, s * -6f));
            }
            Part(PrimitiveType.Cube, parent, new Vector3(0.15f, 0.18f, -0.23f), new Vector3(0.09f, 0.04f, 0.45f), c, true, new Vector3(20f, -25f, 0f));
        }
        void BuildClone(Color ownerColor)
        {
            var c = Color.Lerp(ownerColor, new Color(0.35f, 0.72f, 1f), 0.6f); c.a = 0.34f;
            Ninja(transform, c);
            var faint = c; faint.a = 0.075f;
            echoL = Pivot("Afterimage L", transform, new Vector3(-0.16f, 0f, -0.14f)); Ninja(echoL, faint);
            echoR = Pivot("Afterimage R", transform, new Vector3(0.18f, 0f, -0.27f)); Ninja(echoR, faint);
            Part(PrimitiveType.Cube, transform, new Vector3(0f, 0.40f, 0.151f), new Vector3(0.23f, 0.035f, 0.02f), new Color(0.78f, 1f, 1f, 0.8f), true);
        }

        public void Hurt() => hurtUntil = Time.time + 0.16f;
        public void SetPose(Vector3 position, Quaternion rotation, float clock, float health)
        {
            transform.SetPositionAndRotation(position, rotation);
            health = Mathf.Clamp01(health);
            if (health < previousHealth - 0.001f) Hurt();
            previousHealth = health;
            if (wingL != null)
            {
                float flap = Mathf.Sin(clock * (kind=="fairy"?22f:10f)) * (kind=="fairy"?42f:32f);
                wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            if (flame != null) flame.localScale = new Vector3(1f, 1f + 0.08f * Mathf.Sin(clock * 13f), 1f)*(kind=="fairy"?.75f:1f);
            if(kind=="fairy"&&Time.time>=nextFairyLight){SpellFx.FairySprinkle(transform.position);nextFairyLight=Time.time+3f;}
            if (echoL != null)
            {
                echoL.localPosition = new Vector3(-0.17f - 0.04f * Mathf.Sin(clock * 5f), 0f, -0.15f);
                echoR.localPosition = new Vector3(0.20f, 0f, -0.28f - 0.05f * Mathf.Cos(clock * 5f));
            }
            if(kind=="ice")
            {
                float glow=Time.time<actionUntil?1f:0;
                block.SetColor(ColorId,Color.Lerp(Color.white,new Color(1.35f,1.50f,1.65f),glow));foreach(var r in iceFaces)r.SetPropertyBlock(block);
                if(iceHandL){iceHandL.localRotation=Quaternion.Euler(-glow*35,0,-12+Mathf.Sin(clock*4)*8);iceHandR.localRotation=Quaternion.Euler(-glow*35,0,12-Mathf.Sin(clock*4)*8);}
            }
            if (kind != "golem") return;
            var tint = Color.Lerp(new Color(0.94f, 0.32f, 0.24f), Stone, health);
            if (Time.time < hurtUntil) tint = Color.Lerp(tint, Color.white, 0.7f);
            block.SetColor(ColorId, tint);
            foreach (var r in stones) r.SetPropertyBlock(block);
            for (int i = 0; i < cracks.Count; i++)
            {
                bool show = health < 0.80f - (i / 2) * 0.25f;
                if (cracks[i].activeSelf != show) cracks[i].SetActive(show);
            }
        }
    }
}
