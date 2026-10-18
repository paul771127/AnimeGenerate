using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色外觀：用 Unity 基本形狀＋少量程式產生的網格拼出各職業的 Q 版人形，並用程式做簡單動作。
    ///   不需要任何模型、動畫或資源檔；所有部位都在 Create() 裡即時建立。
    ///   座標：腳底在本地 y = 0，面向本地 +Z，總高約 1.75 m（法師帽尖、法杖略高），
    ///   身體寬度大致在戰鬥判定半徑（Fighter.BodyRadius ≈ 0.28 m）內。
    ///   動作：走路（腿、手擺動＋上下起伏）、待機呼吸、詠唱（舉起武器＋腳下光圈）、
    ///   防禦（劍士舉盾，其他職業雙手交叉）、出手、受擊（閃色＋後仰）、倒地。
    ///   關節都有獨立的樞紐物件（肩、肘、手腕、髖、膝），旋轉樞紐即可讓肢體繞關節轉動。
    ///   牧師：白袍金邊、高主教冠、頂端是發光太陽十字的法杖，頭頂漂浮一圈淡淡的光環。
    ///   另有 classId = "dummy"：練習用木頭人（不會走、不會施法；受擊時左右搖晃、倒地時整個翻倒）。
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        // ================================================================ 對外介面
        /// <summary>移動速度（公尺/秒），驅動走路動作；0 = 待機呼吸。</summary>
        public float MoveSpeed { get; set; }
        /// <summary>詠唱進度 0..1：舉起武器、腳下出現職業色光圈；≤ 0 表示沒有在詠唱。</summary>
        public float Charge { get; set; }
        /// <summary>防禦中：劍士舉盾，其他職業雙手交叉護身。</summary>
        public bool Guarding { get; set; }

        public string ClassId => classId;

        // ================================================================ 共用材質（依顏色快取）
        static Shader litShader, glowShader;
        static bool unlit;      // 找不到有光照的 shader 時用 Sprites/Default，並以「下方、側面較暗」假裝陰影
        static readonly Dictionary<Color32, Material> matCache = new Dictionary<Color32, Material>();
        static readonly Dictionary<Color32, Material> glowCache = new Dictionary<Color32, Material>();
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        static readonly Color Skin = new Color(1f, 0.8f, 0.62f);
        static readonly Color Cloth = new Color(0.27f, 0.38f, 0.62f);
        static readonly Color DarkCloth = new Color(0.36f, 0.22f, 0.58f);
        static readonly Color Metal = new Color(0.7f, 0.84f, 0.96f);
        static readonly Color Wood = new Color(0.7f, 0.38f, 0.16f);
        static readonly Color Leather = new Color(0.63f, 0.29f, 0.12f);
        static readonly Color Gold = new Color(1f, 0.72f, 0.12f);
        static readonly Color EyeCol = new Color(0.1f, 0.1f, 0.16f);
        static readonly Color RobeWhite = new Color(0.96f, 0.95f, 0.91f);

        static void InitShaders()
        {
            if (glowShader != null) return;
            glowShader = Shader.Find("Sprites/Default");
            litShader = ToonStyle.LitShader();   // 賽璐珞風格（黑邊＋兩階明暗）；不支援時退回 Legacy Diffuse
            if (litShader == null) litShader = Shader.Find("Mobile/Diffuse");
            if (litShader == null) litShader = Shader.Find("Diffuse");
            unlit = litShader == null;
            if (unlit) litShader = glowShader;
        }

        /// <summary>一般部位的材質。shade：沒有光照時用來假裝陰影的亮度倍率（有光照時忽略）。</summary>
        static Material Mat(Color c, float shade = 1f)
        {
            InitShaders();
            if (unlit) c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
            Color32 key = c;
            if (matCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(litShader) { color = c, name = "Rig " + ColorUtility.ToHtmlStringRGB(c) };
            matCache[key] = m;
            return m;
        }

        /// <summary>發光部位（不受光照，永遠是亮色）。</summary>
        static Material Glow(Color c)
        {
            InitShaders();
            Color32 key = c;
            if (glowCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(glowShader) { color = c, name = "RigGlow " + ColorUtility.ToHtmlStringRGBA(c) };
            glowCache[key] = m;
            return m;
        }

        /// <summary>有光照的 shader 需要光源；AR 場景若沒有平行光就補一盞（只補一次）。</summary>
        static void EnsureLight()
        {
            if (unlit) return;
            foreach (var l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional && l.enabled) return;
            var go = new GameObject("CharacterRig Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            if (RenderSettings.ambientLight.maxColorComponent < 0.35f)
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        // Face paint is layered geometry; only the sculpted skin/sclera needs a hull outline.
        // Keeping ink/iris paint unlit avoids a full-width hull swallowing tiny facial features.
        readonly List<Transform> eyePaint = new List<Transform>();
        readonly List<Transform> upperLids = new List<Transform>();
        Transform mouthOpen, floatingBook;
        readonly List<Transform> motes = new List<Transform>();
        string advancedId;
        public string AdvancedId => advancedId;

        Portraits.Persona facePersona;

        void BuildFace()
        {
            var p=facePersona;head.localRotation=Quaternion.Euler(p.Pitch,p.Yaw,p.Roll);
            var features=AnimeFace.Build(head,advancedId??classId,p,Skin,Mul(Wood,.45f),
                Color.Lerp(clsColor,new Color(.15f,.48f,.58f),.25f),advancedId=="marksman",advancedId=="bishop");
            eyePaint.AddRange(features.Eyes);upperLids.AddRange(features.Lids);mouthOpen=features.Mouth;
            // 耳朵與職業耳飾保留，臉面及鼻尖已整合在截面頭模內。
            for(int side=-1;side<=1;side+=2)
            {
                var ear=Part(PrimitiveType.Sphere,head,new Vector3(side*.137f,-.012f,-.005f),new Vector3(.042f,.070f,.040f),Mat(Skin));
                if(classId=="archer"){ear.transform.localScale=new Vector3(.042f,.105f,.040f);ear.transform.localRotation=Quaternion.Euler(0,0,-side*35);}
                Part(PrimitiveType.Sphere,head,new Vector3(side*.149f,-.012f,.013f),new Vector3(.012f,.037f,.010f),Glow(new Color(.83f,.45f,.33f)));
                if(side<0)Gem(head,new Vector3(side*.15f,-.054f,.009f),.017f,clsColor);
            }
        }

        // Unit sphere/capsule surfaces, shared by every accessory. Pole triangles are not duplicated.
        static Mesh LatheSphere(int segments, int rings, System.Func<Vector3, Vector3> deform, bool capsule = false)
        {
            var vertices = new Vector3[(segments + 1) * (rings + 1)];
            var uv = new Vector2[vertices.Length];
            var tris = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float theta = j * Mathf.PI / rings;
                for (int i = 0; i <= segments; i++)
                {
                    float phi = (i == segments ? 0f : i * Mathf.PI * 2f / segments);
                    var p = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * 0.5f;
                    if (capsule) p.y += j < rings / 2 ? 0.5f : j > rings / 2 ? -0.5f : 0f;
                    int k = j * (segments + 1) + i;
                    vertices[k] = deform != null ? deform(p) : p;
                    uv[k] = new Vector2(i / (float)segments, j / (float)rings);
                    if (j == rings || i == segments) continue;
                    int a = k, b = k + 1, c = k + segments + 1, d = c + 1;
                    if (j > 0) { tris.Add(a); tris.Add(b); tris.Add(c); }
                    if (j < rings - 1) { tris.Add(b); tris.Add(d); tris.Add(c); }
                }
            }
            var mesh = new Mesh { vertices = vertices, uv = uv, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            // Weld normals across the longitude seam and poles (including the sculpted head).
            var sums = new Dictionary<Vector3, Vector3>(); var normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                var key = new Vector3(Mathf.Round(v.x * 100000f), Mathf.Round(v.y * 100000f), Mathf.Round(v.z * 100000f));
                sums.TryGetValue(key, out var n); sums[key] = n + normals[i];
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i]; var key = new Vector3(Mathf.Round(v.x * 100000f), Mathf.Round(v.y * 100000f), Mathf.Round(v.z * 100000f));
                normals[i] = sums[key].normalized;
            }
            mesh.normals = normals; mesh.RecalculateBounds(); return mesh;
        }
        static Mesh DetailPrimitive(PrimitiveType type)
        {
            string key = "Detail" + type;
            if (meshCache.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            if (type == PrimitiveType.Sphere) mesh = LatheSphere(8, 6, null);
            else if (type == PrimitiveType.Capsule) mesh = LatheSphere(8, 8, null, true);
            else if (type == PrimitiveType.Cylinder)
            {
                mesh = Object.Instantiate(Frustum(0.5f, 0.5f, 2f, 10));
                var v = mesh.vertices; for (int i = 0; i < v.Length; i++) v[i].y -= 1f;
                mesh.vertices = v; mesh.RecalculateBounds();
            }
            else return null;
            mesh.name = key; meshCache[key] = mesh; return mesh;
        }

        void Gem(Transform parent, Vector3 pos, float size, Color color)
        {
            Part(PrimitiveType.Cube, parent, pos, new Vector3(size, size * 1.3f, size * 0.55f), Mat(color), new Vector3(0f, 0f, 45f));
        }
        void Cross(Transform parent, Vector3 pos, float size, Material material)
        {
            Part(PrimitiveType.Cube, parent, pos, new Vector3(size * 0.25f, size, 0.010f), material);
            Part(PrimitiveType.Cube, parent, pos + Vector3.up * size * 0.14f, new Vector3(size * 0.65f, size * 0.20f, 0.013f), material);
        }
        void Book(Transform parent, Vector3 pos, Color cover, float scale = 1f)
        {
            var t = Pivot("Grimoire", parent, pos); t.localScale = Vector3.one * scale;
            Part(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.12f, 0.17f, 0.040f), Mat(RobeWhite));
            for (int s = -1; s <= 1; s += 2)
                Part(PrimitiveType.Cube, t, new Vector3(0f,0f,s*0.027f), new Vector3(0.135f,0.18f,0.010f),Mat(cover));
            Part(PrimitiveType.Cube,t,new Vector3(-0.064f,0f,0f),new Vector3(0.018f,0.18f,0.06f),Mat(Gold));
            Gem(t,new Vector3(0f,0f,0.036f),0.035f,Gold);
        }
        void MakeCape(Color color, float length)
        {
            if (cape != null) RemoveVisual(cape.gameObject);
            cape = Pivot("Cape", spine, new Vector3(0f, 0.51f, -0.14f));
            Part(PrimitiveType.Cube,cape,new Vector3(0f,-length*0.5f,0f),new Vector3(0.39f,length,0.018f),Mat(color));
            for(int s=-1;s<=1;s+=2)
                Part(PrimitiveType.Cube,cape,new Vector3(s*0.18f,-length*0.5f,-0.012f),new Vector3(0.013f,length,0.008f),Mat(Gold));
        }
        static void RemoveVisual(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        void RemoveNamed(Transform parent, string name)
        {
            var t = parent.Find(name); if (t != null) RemoveVisual(t.gameObject);
        }

        void BuildAccessories()
        {
            var trim = Mat(Gold); var strap = Mat(Leather);
            // Belt buckle, wrist plates, knee caps, boot cuffs, and satchel are joint-mounted.
            Part(PrimitiveType.Cube,spine,new Vector3(0f,0.09f,0.132f),new Vector3(0.073f,0.050f,0.021f),trim);
            Gem(spine,new Vector3(0f,0.09f,0.148f),0.027f,clsColor);
            Book(spine,new Vector3(-0.20f,-0.07f,0.02f),Leather,0.70f);
            foreach(var t in new[]{elL,elR})
            {
                Part(PrimitiveType.Cylinder,t,new Vector3(0f,-0.18f,0f),new Vector3(0.092f,0.020f,0.092f),trim);
                Part(PrimitiveType.Cube,t,new Vector3(0f,-0.12f,0.046f),new Vector3(0.063f,0.10f,0.020f),Mat(Metal));
            }
            foreach(var t in new[]{knL,knR})
            {
                Part(PrimitiveType.Sphere,t,new Vector3(0f,0f,0.047f),new Vector3(0.090f,0.085f,0.030f),Mat(Metal));
                Part(PrimitiveType.Cylinder,t,new Vector3(0f,-0.10f,0f),new Vector3(0.112f,0.018f,0.112f),trim);
                Part(PrimitiveType.Cube,t,new Vector3(0f,-0.31f,0.057f),new Vector3(0.045f,0.030f,0.016f),trim);
            }
            Seg(spine,new Vector3(-0.11f,0.49f,0.112f),new Vector3(0f,0.38f,0.135f),0.007f,trim);
            Seg(spine,new Vector3(0.11f,0.49f,0.112f),new Vector3(0f,0.38f,0.135f),0.007f,trim);
            Gem(spine,new Vector3(0f,0.36f,0.14f),0.032f,clsColor);
            if(classId=="mage")
            {
                Book(spine,new Vector3(0.21f,-0.09f,0.04f),Mul(clsColor,0.75f));
                for(int s=-1;s<=1;s+=2)
                {
                    Part(PrimitiveType.Cylinder,spine,new Vector3(s*0.095f,-0.05f,0.20f),new Vector3(0.018f,0.20f,0.018f),Mat(Gold),new Vector3(-6f,0f,0f));
                    for(int i=0;i<3;i++) Gem(spine,new Vector3(s*0.10f,-0.18f-i*0.18f,0.225f+i*0.015f),0.021f,Gold);
                }
                var hat=head.Find("HatCone"); if(hat!=null) StarBadge(hat,new Vector3(0f,0.09f,0.12f),0.048f);
                var staff=handR.Find("Staff"); if(staff!=null) StaffCage(staff,0.95f,0.095f);
                Part(PrimitiveType.Cylinder,spine,new Vector3(0.22f,-0.12f,-0.02f),new Vector3(0.055f,0.11f,0.055f),Mat(RobeWhite));
            }
            else if(classId=="archer")
            {
                Seg(spine,new Vector3(-0.12f,0.50f,0.12f),new Vector3(0.12f,0.10f,0.13f),0.022f,strap);
                Gem(spine,new Vector3(0.03f,0.27f,0.145f),0.032f,Gold);
                for(int i=0;i<3;i++) Gem(cape,new Vector3((i-1)*0.095f,-0.25f,-0.018f),0.040f,clsColor);
                var bow=handL.Find("Bow"); if(bow!=null)
                    for(int i=-2;i<=2;i++) Part(PrimitiveType.Cylinder,bow,new Vector3(0f,0f,i*0.024f),new Vector3(0.044f,0.003f,0.044f),trim,new Vector3(90f,0f,0f));
                Dagger(Pivot("HuntingKnife",spine,new Vector3(0.18f,-0.13f,0.04f)));
            }
            else if(classId=="assassin")
            {
                for(int s=-1;s<=1;s+=2)
                {
                    Seg(spine,new Vector3(s*0.12f,0.49f,0.13f),new Vector3(-s*0.12f,0.11f,0.13f),0.017f,strap);
                    Part(PrimitiveType.Cube,hipL,new Vector3(0.06f,-0.26f,0.06f),new Vector3(0.06f,0.14f,0.026f),strap);
                    Part(PrimitiveType.Sphere,spine,new Vector3(s*0.13f,-0.02f,-0.12f),new Vector3(0.067f,0.080f,0.067f),Mat(Mul(clsColor,0.7f)));
                    Gem(handR,new Vector3(s*0.04f,-0.04f,0.055f),0.020f,Gold);
                }
            }
            else if(classId=="swordsman")
            {
                Cross(spine,new Vector3(0f,0.34f,0.17f),0.11f,trim);
                for(int s=-1;s<=1;s+=2)
                {
                    var shoulder=s<0?shL:shR;
                    for(int i=0;i<2;i++) Part(PrimitiveType.Sphere,shoulder,new Vector3(s*0.035f,-0.045f-i*0.040f,0f),new Vector3(0.18f-i*0.02f,0.075f,0.17f),Mat(i==0?Metal:clsColor));
                }
                ShieldDetails(false);
                var sword=handR.Find("Sword"); if(sword!=null)
                {
                    Gem(sword,new Vector3(0f,0.022f,0.07f),0.032f,clsColor);
                    Seg(sword,new Vector3(0f,0f,-0.09f),new Vector3(0.08f,0f,-0.21f),0.012f,Mat(clsColor));
                }
            }
            else
            {
                Book(spine,new Vector3(0.21f,-0.08f,0.05f),new Color(0.67f,0.17f,0.23f));
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI/6f;
                    Part(PrimitiveType.Sphere,spine,new Vector3(0.14f+Mathf.Cos(a)*0.060f,-0.09f-Mathf.Sin(a)*0.11f,0.12f),Vector3.one*0.018f,trim);
                }
                Cross(spine,new Vector3(0f,0.47f,0.15f),0.07f,trim);
                if(halo!=null) { halo.localScale=Vector3.one*1.1f; }
            }
        }
        void StarBadge(Transform parent,Vector3 pos,float size)
        {
            for(int i=0;i<5;i++)
            {
                float a=i*72f*Mathf.Deg2Rad;
                Seg(parent,pos,pos+new Vector3(Mathf.Sin(a),Mathf.Cos(a),0f)*size,0.009f,Mat(Gold));
            }
        }
        void StaffCage(Transform staff,float z,float radius)
        {
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*0.5f;
                Seg(staff,new Vector3(0f,0f,z-0.14f),new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,z),0.012f,Mat(Gold));
            }
        }
        void ShieldDetails(bool cross)
        {
            if(shield==null) return;
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4f;
                Part(PrimitiveType.Sphere,shield,new Vector3(Mathf.Cos(a)*0.17f,0.028f,Mathf.Sin(a)*0.17f),Vector3.one*0.019f,Mat(Gold));
            }
            if(cross)
            {
                var badge=Pivot("ShieldCross",shield,new Vector3(0f,0.035f,0f)); badge.localRotation=Quaternion.Euler(90f,0f,0f);
                Cross(badge,Vector3.zero,0.23f,Mat(RobeWhite));
            }
        }

        void BuildAdvanced()
        {
            if(string.IsNullOrEmpty(advancedId)) return;
            switch(advancedId)
            {
                case "paladin":
                    RepaintArmor(RobeWhite); MakeCape(new Color(0.82f,0.15f,0.20f),0.92f);
                    if(shield!=null) { shield.localScale=Vector3.one*1.30f; ShieldDetails(true); }
                    Cross(spine,new Vector3(0f,0.34f,0.185f),0.15f,Mat(Gold));
                    break;
                case "berserker":
                    RemoveNamed(handR,"Sword"); if(shield!=null) { RemoveVisual(shield.gameObject); shield=null; }
                    MakeCape(new Color(0.39f,0.18f,0.25f),0.47f);
                    for(int s=-1;s<=1;s+=2)
                    {
                        var t=s<0?shL:shR;
                        for(int i=0;i<3;i++) Part(PrimitiveType.Sphere,t,new Vector3(s*0.035f,-i*0.035f,-0.02f),new Vector3(0.19f,0.10f,0.20f),Mat(new Color(0.69f,0.49f,0.28f)));
                    }
                    BuildAxe(); break;
                case "marksman":
                    MakeCape(new Color(0.30f,0.40f,0.57f),1.05f);
                    var bow=handL.Find("Bow"); if(bow!=null) bow.localScale=new Vector3(1f,1.2f,1.3f);
                    Part(PrimitiveType.Cylinder,head,new Vector3(-0.054f,0.018f,0.158f),new Vector3(0.065f,0.012f,0.065f),Mat(Gold),new Vector3(90f,0f,0f));
                    Part(PrimitiveType.Sphere,head,new Vector3(-0.054f,0.018f,0.175f),new Vector3(0.046f,0.045f,0.007f),Glow(new Color(0.25f,0.91f,1f,0.65f)));
                    break;
                case "ranger":
                    MakeCape(new Color(0.21f,0.55f,0.33f),0.68f); ShoulderFalcon();
                    var trap=MeshPart("BeltTrap",spine,RingMesh(0.038f,0.052f,12),new Vector3(0.21f,0.02f,0.08f),Mat(Metal),new Vector3(90f,0f,0f));
                    trap.transform.localScale=Vector3.one*1.1f; break;
                case "archmage":
                    var hat=head.Find("HatCone"); if(hat!=null) hat.localScale=new Vector3(1.08f,1.45f,1.08f);
                    floatingBook=Pivot("FloatingBook",spine,new Vector3(-0.41f,0.31f,0.05f));
                    Book(floatingBook,Vector3.zero,new Color(0.43f,0.22f,0.75f),1.35f);
                    UpgradeCrystal(0.19f); break;
                case "summoner":
                    Book(spine,new Vector3(0.25f,-0.05f,0.10f),new Color(0.86f,0.27f,0.13f),1.1f);
                    for(int i=0;i<3;i++)
                    {
                        var t=Part(PrimitiveType.Sphere,spine,new Vector3(0.34f*Mathf.Cos(i*2.1f),0.34f,0.24f*Mathf.Sin(i*2.1f)),Vector3.one*0.070f,
                            Glow(i==0?new Color(1f,0.48f,0.14f):i==1?new Color(0.33f,0.87f,1f):new Color(0.63f,1f,0.36f))).transform;
                        motes.Add(t);
                    }
                    break;
                case "ninja":
                    var back=Pivot("NinjaSword",spine,new Vector3(0f,0.26f,-0.18f)); back.localRotation=Quaternion.Euler(0f,25f,35f);
                    Part(PrimitiveType.Cube,back,Vector3.zero,new Vector3(0.040f,0.65f,0.035f),Mat(new Color(0.24f,0.33f,0.49f)));
                    Part(PrimitiveType.Cube,back,new Vector3(0f,0.25f,0f),new Vector3(0.14f,0.025f,0.045f),Mat(Gold));
                    StarBadge(spine,new Vector3(0.17f,0.08f,0.12f),0.060f); break;
                case "shadowdancer":
                    for(int s=-1;s<=1;s+=2)
                    {
                        var ribbon=Pivot("DanceRibbon",spine,new Vector3(s*0.12f,0.20f,-0.11f));
                        Part(PrimitiveType.Cube,ribbon,new Vector3(s*0.09f,-0.30f,0f),new Vector3(0.055f,0.62f,0.012f),Mat(clsColor),new Vector3(0f,0f,s*18f));
                        var hand=s<0?handL:handR; RemoveNamed(hand,"Dagger");
                        var blade=Pivot("CrescentBlade",hand,new Vector3(0f,-0.04f,0f));
                        for(int i=0;i<6;i++)
                        {
                            float a=i*0.27f;
                            Seg(blade,new Vector3(0.13f*(1f-Mathf.Cos(a)),0f,0.04f+i*0.05f),new Vector3(0.13f*(1f-Mathf.Cos(a+0.27f)),0f,0.09f+i*0.05f),0.028f,Mat(Metal));
                        }
                    }
                    break;
                case "bishop":
                    var crown=head.Find("Mitre"); if(crown!=null) crown.localScale=new Vector3(1.12f,1.32f,1.05f);
                    MakeCape(new Color(0.93f,0.83f,0.44f),0.88f);
                    foreach(var shoulder in new[]{shL,shR}) Part(PrimitiveType.Sphere,shoulder,Vector3.zero,new Vector3(0.22f,0.13f,0.24f),Mat(RobeWhite));
                    var staff=handR.Find("Staff"); if(staff!=null) { staff.localScale=Vector3.one*1.13f; Cross(staff,new Vector3(0f,-0.015f,1f),0.24f,Mat(Gold)); }
                    break;
                case "inquisitor":
                    RemoveNamed(handR,"Staff"); orb=null; RemoveNamed(head,"Mitre");
                    foreach(var shoulder in new[]{shL,shR}) Part(PrimitiveType.Sphere,shoulder,Vector3.zero,new Vector3(0.23f,0.17f,0.24f),Mat(Gold));
                    var lance=Pivot("HolyLance",handR,new Vector3(0f,-0.04f,0f));
                    Part(PrimitiveType.Cylinder,lance,new Vector3(0f,0f,0.30f),new Vector3(0.025f,0.83f,0.025f),Mat(Gold),new Vector3(90f,0f,0f));
                    MeshPart("LancePoint",lance,Frustum(0.065f,0f,0.31f,4),new Vector3(0f,0f,1.12f),Mat(RobeWhite),new Vector3(90f,0f,0f));
                    Seg(spine,new Vector3(-0.075f,0.35f,0.17f),new Vector3(0.075f,0.35f,0.17f),0.009f,Mat(Gold));
                    for(int s=-1;s<=1;s+=2) { Seg(spine,new Vector3(s*0.07f,0.35f,0.17f),new Vector3(s*0.07f,0.29f,0.17f),0.005f,Mat(Gold));
                        Part(PrimitiveType.Sphere,spine,new Vector3(s*0.07f,0.28f,0.17f),new Vector3(0.065f,0.018f,0.035f),Mat(Gold)); }
                    break;
            }
        }
        void RepaintArmor(Color color)
        {
            foreach(var r in spine.GetComponentsInChildren<Renderer>())
                if(r.sharedMaterial==Mat(Metal)||r.sharedMaterial==Mat(Cloth)) r.sharedMaterial=Mat(color);
        }
        void UpgradeCrystal(float size)
        {
            if(orb==null) return;
            orb.localScale=Vector3.one*size; orbBaseScale=orb.localScale;
            var staff=handR.Find("Staff"); if(staff!=null) StaffCage(staff,0.95f,size*0.70f);
        }
        void BuildAxe()
        {
            var axe=Pivot("WarAxe",handR,new Vector3(0f,-0.04f,0f));
            // Cant the long haft out from the shoulder, leaving the face visible in idle/cast poses.
            axe.localRotation=Quaternion.Euler(0f,30f,0f);
            Part(PrimitiveType.Cylinder,axe,new Vector3(0f,0f,0.28f),new Vector3(0.045f,0.50f,0.045f),Mat(Wood),new Vector3(90f,0f,0f));
            for(int s=-1;s<=1;s+=2)
                Part(PrimitiveType.Cube,axe,new Vector3(s*0.17f,0f,0.66f),new Vector3(0.29f,0.065f,0.26f),Mat(Metal),new Vector3(0f,s*18f,0f));
            Gem(axe,new Vector3(0f,0.04f,0.65f),0.065f,clsColor);
        }
        void ShoulderFalcon()
        {
            var t=Pivot("PerchedFalcon",shR,new Vector3(0.02f,0.15f,0f));
            Part(PrimitiveType.Sphere,t,Vector3.zero,new Vector3(0.10f,0.14f,0.13f),Mat(Wood));
            Part(PrimitiveType.Sphere,t,new Vector3(0f,0.085f,0.025f),Vector3.one*0.075f,Mat(RobeWhite));
            for(int s=-1;s<=1;s+=2) Part(PrimitiveType.Sphere,t,new Vector3(s*0.045f,-0.005f,-0.015f),new Vector3(0.035f,0.11f,0.09f),Mat(Leather));
            Part(PrimitiveType.Cube,t,new Vector3(0f,0.072f,0.073f),new Vector3(0.025f,0.020f,0.045f),Mat(Gold));
        }

        void BuildPersonaClothing()
        {
            // Small additions to the existing costumes; animation pivots and material caches are reused.
            if(classId=="swordsman")
                foreach(var shoulder in new[]{shL,shR})Part(PrimitiveType.Sphere,shoulder,new Vector3(0,.04f,.02f),new Vector3(.23f,.10f,.25f),Mat(Metal));
            if(classId=="mage")
                for(int side=-1;side<=1;side+=2)Seg(spine,new Vector3(side*.05f,.47f,.14f),new Vector3(side*.14f,.56f,.10f),.025f,Mat(Gold));
            if(classId=="archer")
                Seg(spine,new Vector3(-.13f,.43f,.13f),new Vector3(.14f,.09f,.15f),.020f,Mat(Leather));
            if(classId=="assassin")
                Part(PrimitiveType.Sphere,spine,new Vector3(0,.52f,.06f),new Vector3(.24f,.13f,.22f),Mat(DarkCloth));
            if(classId=="priest")
                for(int side=-1;side<=1;side+=2)Seg(spine,new Vector3(side*.065f,.39f,.15f),new Vector3(side*.065f,.12f,.14f),.012f,Mat(Gold));
            Color hair=classId=="mage"?new Color(.92f,.92f,.95f):classId=="priest"?new Color(.93f,.82f,.55f):classId=="assassin"?DarkCloth:new Color(.55f,.35f,.20f);
            AnimeFace.Hair(head,advancedId??classId,facePersona,hair);
            if(advancedId=="berserker")
            {
                Seg(head,new Vector3(.072f,.013f,.126f),new Vector3(.084f,-.037f,.116f),.004f,Glow(new Color(.63f,.22f,.20f)));
                Seg(shR,new Vector3(-.06f,.05f,.09f),new Vector3(.07f,-.03f,.10f),.007f,Mat(Leather));
            }
            if(advancedId=="ranger")
                for(int side=-1;side<=1;side+=2)Part(PrimitiveType.Sphere,head,new Vector3(side*.105f,.115f,.07f),new Vector3(.04f,.10f,.055f),Mat(Wood));
            if(advancedId=="ninja")Part(PrimitiveType.Sphere,head,new Vector3(0,-.085f,.108f),new Vector3(.23f,.115f,.07f),Mat(DarkCloth));
            if(advancedId=="shadowdancer")
                for(int side=-1;side<=1;side+=2)Seg(head,new Vector3(side*.077f,.032f,.128f),new Vector3(side*.105f,.045f,.113f),.004f,Glow(EyeCol));
            if(advancedId=="bishop")
                for(int side=-1;side<=1;side+=2)Seg(head,new Vector3(side*.080f,-.005f,.126f),new Vector3(side*.102f,-.016f,.114f),.003f,Glow(Mul(Skin,.60f)));
        }

        void AnimateDetails(float cast, float hurt)
        {
            float squint=1f-0.82f*hurt;
            foreach(var eye in eyePaint) eye.localScale=new Vector3(1f,squint,1f);
            foreach(var lid in upperLids) lid.localPosition=new Vector3(lid.localPosition.x,0.037f-0.018f*hurt,0.122f);
            if(mouthOpen!=null) mouthOpen.localScale=new Vector3(.027f+.035f*facePersona.Open,.001f+.024f*facePersona.Open+.017f*Mathf.Max(cast,chargeAmt*.45f),.003f);
            if(floatingBook!=null) { floatingBook.localPosition=new Vector3(-0.41f,0.31f+0.025f*Mathf.Sin(clock*2f),0.05f); floatingBook.localRotation=Quaternion.Euler(0f,clock*14f,8f); }
            for(int i=0;i<motes.Count;i++)
            {
                float a=clock*1.5f+i*2.1f;
                motes[i].localPosition=new Vector3(Mathf.Cos(a)*0.34f,0.34f+0.08f*Mathf.Sin(a*1.7f),Mathf.Sin(a)*0.24f);
            }
        }


        // ================================================================ 骨架
        string classId;
        Color clsColor;
        Transform body, spine, neck, head;
        Transform shL, shR, elL, elR, handL, handR;
        Transform hipL, hipR, knL, knR;
        Transform cape, scarfTail, shield, orb, halo;
        Vector3 shieldIdlePos; Quaternion shieldIdleRot;
        Vector3 orbBaseScale, haloBasePos;
        GameObject aura; Material auraMat;

        Renderer[] rends; Material[] origMats;
        Material flashMat;
        bool visible = true, flashing;

        // 動作狀態
        float walkPhase, walkAmt, guardAmt, chargeAmt, castT = -1f, hitT, deadAmt, clock;
        bool dead;
        float legAmpScale = 1f;

        struct Arm
        {
            public float x, zOut, el, wr;
            public Arm(float x, float zOut, float el, float wr) { this.x = x; this.zOut = zOut; this.el = el; this.wr = wr; }
            public static Arm Lerp(Arm a, Arm b, float t) =>
                new Arm(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.zOut, b.zOut, t), Mathf.Lerp(a.el, b.el, t), Mathf.Lerp(a.wr, b.wr, t));
        }
        Arm baseL, baseR, guardL, guardR, chargeL, chargeR, castL, castR;
        float castMaskL, castMaskR, swingL = 1f, swingR = 1f;
        static readonly Arm Relaxed = new Arm(0f, 25f, -10f, 0f);

        const float CastDur = 0.3f, HitDur = 0.15f, DieDur = 0.6f;

        // ================================================================ 建立
        public static CharacterRig Create(string classId, Transform parent = null) => Create(classId, (string)null, parent);

        /// <summary>Advanced appearance only; invalid or mismatched advanced ids use the base rig.</summary>
        public static CharacterRig Create(string classId, string advId, Transform parent = null)
        {
            InitShaders();
            EnsureLight();
            if (classId != DummyId && (string.IsNullOrEmpty(classId) || !Skills.Classes.ContainsKey(classId))) classId = "mage";
            var appearance = Advanced.Get(advId);
            string validAdv = appearance != null && appearance.baseCls == classId ? advId : null;
            var go = new GameObject($"Character {classId} {validAdv}");
            if (parent != null) go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CharacterRig>();
            rig.advancedId = validAdv;
            rig.Build(classId);
            return rig;
        }

        void Build(string id)
        {
            classId = id;
            facePersona=Portraits.PersonaFor(advancedId??id);
            if (id == DummyId) { BuildDummy(); return; }
            clsColor = Advanced.Get(advancedId)?.color ?? Skills.Classes[id].color; clsColor.a = 1f;
            bool slim = id == "assassin";
            float sw = slim ? 0.85f : 1f;

            // 各職業的配色
            Color torsoC, armC, pantsC, bootC;
            switch (id)
            {
                case "mage": torsoC = clsColor; armC = clsColor; pantsC = Cloth; bootC = Leather; break;
                case "archer": torsoC = Mul(clsColor, 0.8f); armC = Mul(clsColor, 0.8f); pantsC = Leather; bootC = Mul(Leather, 0.7f); break;
                case "assassin": torsoC = DarkCloth; armC = DarkCloth; pantsC = DarkCloth; bootC = Mul(DarkCloth, 0.8f); break;
                case "priest": torsoC = RobeWhite; armC = RobeWhite; pantsC = Mul(RobeWhite, 0.9f); bootC = Mul(Gold, 0.75f); break;
                default: torsoC = Metal; armC = Cloth; pantsC = Cloth; bootC = Metal; break;
            }

            switch (advancedId)
            {
                case "paladin": torsoC = armC = RobeWhite; bootC = Gold; break;
                case "berserker": torsoC = clsColor; armC = Leather; pantsC = new Color(0.31f, 0.19f, 0.27f); break;
                case "ninja": torsoC = armC = pantsC = new Color(0.29f, 0.40f, 0.57f); bootC = Metal; break;
                case "shadowdancer": torsoC = armC = Mul(clsColor, 0.75f); pantsC = DarkCloth; break;
                case "inquisitor": torsoC = armC = clsColor; bootC = Gold; break;
            }

            body = Pivot("Body", transform, Vector3.zero);

            // ---- 腿（髖 → 膝）
            hipL = Leg("L", -1f, sw, pantsC, bootC, out knL);
            hipR = Leg("R", 1f, sw, pantsC, bootC, out knR);

            // ---- 軀幹（以髖部高度為樞紐，前傾 / 後仰都繞這裡轉）
            var posture=Pivot("PersonaStance",body,new Vector3(0,.85f,0));
            posture.localRotation=Quaternion.Euler(facePersona.Lean,0,facePersona.Roll*.12f);
            spine = Pivot("Spine", posture, Vector3.zero);
            Part(PrimitiveType.Sphere, spine, new Vector3(0f, 0.03f, 0f), new Vector3(0.3f * sw, 0.18f, 0.2f), Mat(pantsC, 0.85f));
            Part(PrimitiveType.Capsule, spine, new Vector3(0f, 0.3f, 0f), new Vector3(0.34f * sw, 0.27f, 0.22f), Mat(torsoC));
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.08f, 0f), new Vector3(0.35f * sw, 0.03f, 0.24f), Mat(id == "mage" || id == "priest" ? Gold : Leather, 0.9f));
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.56f, 0f), new Vector3(0.08f, 0.04f, 0.08f), Mat(Skin, 0.85f));

            // ---- 頭（脖子樞紐 → 頭中心）
            neck = Pivot("Neck", spine, new Vector3(0f, 0.57f, 0f));
            head = Pivot("Head", neck, new Vector3(0f, 0.14f, 0f));
            BuildFace();

            // ---- 手臂（肩 → 肘 → 手腕）
            shL = ArmChain("L", -1f, sw, armC, out elL, out handL);
            shR = ArmChain("R", 1f, sw, armC, out elR, out handR);

            // 預設姿勢：手自然下垂
            var hang = new Arm(0f, 8f, -15f, 0f);
            baseL = baseR = hang;
            var cross = new Arm(-70f, -30f, -75f, 0f);
            guardL = guardR = cross;
            chargeL = chargeR = new Arm(-60f, 10f, -40f, 0f);
            castL = castR = new Arm(-85f, 0f, 0f, 60f);
            castMaskL = 0f; castMaskR = 1f;

            switch (id)
            {
                case "mage": BuildMage(); break;
                case "archer": BuildArcher(); break;
                case "assassin": BuildAssassin(); break;
                case "priest": BuildPriest(); break;
                default: BuildSwordsman(); break;
            }

            BuildAccessories();
            BuildAdvanced();
            BuildPersonaClothing();

            // ---- 詠唱光圈（不跟身體倒下，固定在腳底）
            auraMat = new Material(glowShader) { color = new Color(clsColor.r, clsColor.g, clsColor.b, 0f), name = "RigAura" };
            aura = new GameObject("Aura");
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            aura.AddComponent<MeshFilter>().sharedMesh = RingMesh(0.36f, 0.45f, 40);
            var ar = aura.AddComponent<MeshRenderer>();
            ar.sharedMaterial = auraMat;
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aura.SetActive(false);

            // 受擊閃色用：記住每個部位原本的材質
            flashMat = Glow(new Color(1f, 0.55f, 0.5f));
            var all = body.GetComponentsInChildren<Renderer>(true);
            // 僅人形換材質；木頭人提前返回，Boss／小怪完全不使用此路徑。
            foreach(var renderer in all)
            {
                var material=renderer.sharedMaterial;
                if(material.name!=null&&material.name.StartsWith("Rig "))
                    renderer.sharedMaterial=AnimeFace.Material(material.color,ColorUtility.ToHtmlStringRGB(material.color)==ColorUtility.ToHtmlStringRGB(Skin)?2:0);
            }
            rends = all;
            origMats = new Material[all.Length];
            for (int i = 0; i < all.Length; i++) origMats[i] = all[i].sharedMaterial;

            Animate(0f);
        }

        Transform Leg(string side, float s, float sw, Color pantsC, Color bootC, out Transform knee)
        {
            var hip = Pivot("Hip" + side, body, new Vector3(0.085f * sw * s, 0.85f, 0f));
            Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.2f, 0f), new Vector3(0.12f * sw, 0.22f, 0.12f * sw), Mat(pantsC, 0.8f));
            knee = Pivot("Knee" + side, hip, new Vector3(0f, -0.42f, 0f));
            Part(PrimitiveType.Capsule, knee, new Vector3(0f, -0.2f, 0f), new Vector3(0.1f * sw, 0.21f, 0.1f * sw), Mat(bootC, 0.72f));
            Part(PrimitiveType.Cube, knee, new Vector3(0f, -0.4f, 0.04f), new Vector3(0.1f * sw, 0.06f, 0.2f), Mat(bootC, 0.62f));
            return hip;
        }

        Transform ArmChain(string side, float s, float sw, Color armC, out Transform elbow, out Transform hand)
        {
            var sh = Pivot("Shoulder" + side, spine, new Vector3(0.2f * sw * s, 0.5f, 0f));
            Part(PrimitiveType.Capsule, sh, new Vector3(0f, -0.14f, 0f), new Vector3(0.085f * sw, 0.15f, 0.085f * sw), Mat(armC, 0.9f));
            elbow = Pivot("Elbow" + side, sh, new Vector3(0f, -0.28f, 0f));
            Part(PrimitiveType.Capsule, elbow, new Vector3(0f, -0.12f, 0f), new Vector3(0.075f * sw, 0.13f, 0.075f * sw), Mat(armC, 0.85f));
            hand = Pivot("Hand" + side, elbow, new Vector3(0f, -0.25f, 0f));
            Part(PrimitiveType.Sphere, hand, new Vector3(0f, -0.03f, 0f), new Vector3(0.085f, 0.085f, 0.085f), Mat(Skin, 0.9f));
            return sh;
        }

        // ---------------------------------------------------------------- 法師：尖帽、長袍、法杖
        void BuildMage()
        {
            var hatC = Mul(clsColor, 0.85f);
            // 帽子：帽沿＋圓錐（帽尖稍微往後彎）
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.07f, -0.01f), new Vector3(0.46f, 0.008f, 0.46f), Mat(hatC, 0.85f));
            var cone = Pivot("HatCone", head, new Vector3(0f, 0.075f, -0.01f));
            cone.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            MeshPart("Hat", cone, Frustum(0.165f, 0f, 0.27f, 20), Vector3.zero, Mat(hatC));
            MeshPart("HatBand", cone, Frustum(0.168f, 0.15f, 0.04f, 20), new Vector3(0f, 0.005f, 0f), Mat(Gold, 0.9f));
            // 頭髮（後腦）
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.02f, -0.045f), new Vector3(0.29f, 0.26f, 0.24f), Mat(new Color(0.92f, 0.92f, 0.95f), 0.9f));
            // 長袍：從腰到腳踝的喇叭狀圓台，袍擺鑲金邊
            MeshPart("Robe", spine, Frustum(0.25f, 0.16f, 0.95f, 24), new Vector3(0f, -0.8f, 0f), Mat(clsColor, 0.8f));
            MeshPart("RobeHem", spine, Frustum(0.255f, 0.245f, 0.05f, 24), new Vector3(0f, -0.81f, 0f), Mat(Gold, 0.75f));
            // 袖口
            MeshPart("CuffL", elL, Frustum(0.07f, 0.05f, 0.08f, 12), new Vector3(0f, -0.25f, 0f), Mat(Gold, 0.85f));
            MeshPart("CuffR", elR, Frustum(0.07f, 0.05f, 0.08f, 12), new Vector3(0f, -0.25f, 0f), Mat(Gold, 0.85f));
            // 法杖（沿手的 +Z，前臂水平時法杖直立）＋頂端發光寶珠
            var staff = Pivot("Staff", handR, new Vector3(0f, -0.04f, 0f));
            Part(PrimitiveType.Cylinder, staff, new Vector3(0f, 0f, 0.2f), new Vector3(0.035f, 0.65f, 0.035f), Mat(Wood, 0.85f), new Vector3(90f, 0f, 0f));
            MeshPart("StaffHead", staff, Frustum(0.025f, 0.07f, 0.08f, 12), new Vector3(0f, 0f, 0.82f), Mat(Gold), new Vector3(90f, 0f, 0f));
            orb = Part(PrimitiveType.Sphere, staff, new Vector3(0f, 0f, 0.95f), new Vector3(0.11f, 0.11f, 0.11f),
                Glow(Color.Lerp(clsColor, Color.white, 0.45f))).transform;
            orbBaseScale = orb.localScale;

            baseR = new Arm(-10f, 6f, -80f, 0f); swingR = 0.35f;
            chargeR = new Arm(-70f, 5f, -20f, 0f);
            chargeL = new Arm(-75f, -5f, -30f, 0f);
            castR = new Arm(-95f, 0f, -5f, 60f);
            legAmpScale = 0.6f;
        }

        // ---------------------------------------------------------------- 獵人：兜帽披風、弓、箭袋
        void BuildArcher()
        {
            var hoodC = Mul(clsColor, 0.78f);
            // 頭髮瀏海＋兜帽（往後偏，臉露出來）＋帽尖
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.075f, 0.02f), new Vector3(0.27f, 0.17f, 0.25f), Mat(new Color(0.55f, 0.35f, 0.2f)));
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.025f, -0.05f), new Vector3(0.33f, 0.34f, 0.33f), Mat(hoodC));
            var tip = Pivot("HoodTip", head, new Vector3(0f, 0.04f, -0.17f));
            tip.localRotation = Quaternion.Euler(-120f, 0f, 0f);
            MeshPart("HoodTipCone", tip, Frustum(0.08f, 0f, 0.14f, 12), Vector3.zero, Mat(hoodC, 0.85f));
            // 披風（掛在肩膀後方，走路時往後飄）
            cape = Pivot("Cape", spine, new Vector3(0f, 0.52f, -0.12f));
            Part(PrimitiveType.Cube, cape, new Vector3(0f, -0.36f, 0f), new Vector3(0.36f, 0.72f, 0.02f), Mat(hoodC, 0.8f));
            // 短裙擺
            MeshPart("Skirt", spine, Frustum(0.2f, 0.175f, 0.22f, 20), new Vector3(0f, -0.15f, 0f), Mat(clsColor, 0.8f));
            // 箭袋（背上斜背）＋三支箭
            var q = Pivot("Quiver", spine, new Vector3(0.08f, 0.33f, -0.17f));
            q.localRotation = Quaternion.Euler(-10f, 0f, -22f);
            Part(PrimitiveType.Cylinder, q, Vector3.zero, new Vector3(0.1f, 0.17f, 0.1f), Mat(Leather, 0.85f));
            Part(PrimitiveType.Cylinder, q, new Vector3(0f, 0.16f, 0f), new Vector3(0.11f, 0.015f, 0.11f), Mat(Gold, 0.9f));
            for (int i = 0; i < 3; i++)
            {
                var off = new Vector3((i - 1) * 0.025f, 0f, (i == 1 ? 0.015f : -0.01f));
                Part(PrimitiveType.Cylinder, q, off + new Vector3(0f, 0.2f, 0f), new Vector3(0.012f, 0.1f, 0.012f), Mat(Wood));
                Part(PrimitiveType.Cube, q, off + new Vector3(0f, 0.29f, 0f), new Vector3(0.006f, 0.07f, 0.035f), Mat(Color.white, 0.95f));
            }
            // 弓（左手）：前臂水平時弓直立；弓身由小段圓柱拼成弧形，握把在手上，弓梢往後彎，弦在後方
            var bow = Pivot("Bow", handL, new Vector3(0f, -0.04f, 0f));
            const int N = 10; const float half = 0.52f, bend = 0.13f;
            var woodMat = Mat(Wood, 0.9f);
            Vector3 prev = BowPoint(-1f, half, bend);
            for (int i = 1; i <= N; i++)
            {
                var p = BowPoint(-1f + 2f * i / N, half, bend);
                Seg(bow, prev, p, 0.028f, woodMat);
                prev = p;
            }
            Part(PrimitiveType.Cylinder, bow, Vector3.zero, new Vector3(0.04f, 0.06f, 0.04f), Mat(Leather), new Vector3(90f, 0f, 0f));
            Seg(bow, BowPoint(-1f, half, bend), BowPoint(1f, half, bend), 0.006f, Glow(new Color(0.95f, 0.95f, 0.9f)));

            baseL = new Arm(-15f, 8f, -60f, 0f); swingL = 0.5f;
            chargeL = new Arm(-88f, 0f, 0f, 0f);
            chargeR = new Arm(-85f, -25f, -120f, 0f);
            castL = chargeL; castR = new Arm(-40f, 30f, -30f, 0f);
            castMaskL = 1f; castMaskR = 1f;
        }

        static Vector3 BowPoint(float t, float half, float bend) => new Vector3(0f, bend * t * t, half * t);

        // ---------------------------------------------------------------- 刺客：深色兜帽、面罩、雙匕首、圍巾
        void BuildAssassin()
        {
            var hoodC = Color.Lerp(DarkCloth, clsColor, 0.22f);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.025f, -0.05f), new Vector3(0.33f, 0.34f, 0.33f), Mat(hoodC));
            var tip = Pivot("HoodTip", head, new Vector3(0f, 0.08f, -0.12f));
            tip.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            MeshPart("HoodTipCone", tip, Frustum(0.09f, 0f, 0.13f, 12), Vector3.zero, Mat(hoodC, 0.9f));
            // 面罩：蓋住鼻子以下
            if(advancedId!="shadowdancer") Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.092f, 0.010f), new Vector3(0.25f, 0.11f, 0.25f), Mat(Mul(DarkCloth, 0.9f)));
            // 圍巾：脖子一圈＋往後飄的尾巴（職業色）
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.55f, 0f), new Vector3(0.2f, 0.045f, 0.2f), Mat(clsColor));
            scarfTail = Pivot("ScarfTail", spine, new Vector3(0.05f, 0.55f, -0.09f));
            Part(PrimitiveType.Cube, scarfTail, new Vector3(0f, -0.2f, 0f), new Vector3(0.08f, 0.4f, 0.015f), Mat(clsColor, 0.85f));
            // 腰帶（職業色）
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.025f, 0.21f), Mat(clsColor, 0.8f));
            // 雙匕首
            Dagger(handL); Dagger(handR);

            baseL = baseR = new Arm(-25f, 10f, -60f, 20f);
            chargeL = chargeR = new Arm(-40f, 20f, -105f, 10f);
            castR = new Arm(-90f, -5f, 0f, 85f);
            castL = new Arm(-60f, 10f, -40f, 20f);
            castMaskL = 1f; castMaskR = 1f;
            swingL = swingR = 0.6f;
        }

        void Dagger(Transform hand)
        {
            var d = Pivot("Dagger", hand, new Vector3(0f, -0.04f, 0f));
            Part(PrimitiveType.Cylinder, d, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.045f, 0.03f), Mat(clsColor, 0.8f), new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cube, d, new Vector3(0f, 0f, 0.05f), new Vector3(0.025f, 0.09f, 0.02f), Mat(Metal, 0.8f));
            Part(PrimitiveType.Cube, d, new Vector3(0f, 0f, 0.17f), new Vector3(0.012f, 0.04f, 0.22f), Mat(Color.Lerp(Metal, Color.white, 0.4f)));
        }

        // ---------------------------------------------------------------- 劍士：頭盔羽飾、護肩、胸甲、劍、圓盾
        void BuildSwordsman()
        {
            // 頭盔（往後偏，露出臉）＋護鼻＋職業色羽飾
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.045f, -0.025f), new Vector3(0.31f, 0.25f, 0.31f), Mat(Metal));
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.06f, -0.025f), new Vector3(0.32f, 0.012f, 0.32f), Mat(Metal, 0.8f));
            Part(PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.14f), new Vector3(0.022f, 0.08f, 0.02f), Mat(Metal, 0.9f));
            Part(PrimitiveType.Cube, head, new Vector3(0f, 0.17f, -0.03f), new Vector3(0.035f, 0.09f, 0.28f), Mat(clsColor));
            // 胸甲（金屬）＋職業色罩袍＋護肩
            Part(PrimitiveType.Sphere, spine, new Vector3(0f, 0.38f, 0.03f), new Vector3(0.36f, 0.3f, 0.24f), Mat(Color.Lerp(Metal, Color.white, 0.25f)));
            Part(PrimitiveType.Cube, spine, new Vector3(0f, 0.1f, 0.095f), new Vector3(0.2f, 0.3f, 0.03f), Mat(clsColor, 0.85f));
            Part(PrimitiveType.Cube, spine, new Vector3(0f, 0.1f, -0.095f), new Vector3(0.2f, 0.3f, 0.03f), Mat(clsColor, 0.75f));
            Part(PrimitiveType.Sphere, shL, new Vector3(-0.015f, -0.01f, 0f), new Vector3(0.15f, 0.1f, 0.15f), Mat(clsColor));
            Part(PrimitiveType.Sphere, shR, new Vector3(0.015f, -0.01f, 0f), new Vector3(0.15f, 0.1f, 0.15f), Mat(clsColor));
            // 劍（右手）：刀身沿手的 +Z
            var sword = Pivot("Sword", handR, new Vector3(0f, -0.04f, 0f));
            Part(PrimitiveType.Cylinder, sword, new Vector3(0f, 0f, -0.01f), new Vector3(0.035f, 0.07f, 0.035f), Mat(Leather), new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Sphere, sword, new Vector3(0f, 0f, -0.09f), new Vector3(0.05f, 0.05f, 0.05f), Mat(Gold));
            Part(PrimitiveType.Cube, sword, new Vector3(0f, 0f, 0.07f), new Vector3(0.035f, 0.2f, 0.035f), Mat(Gold, 0.9f));
            Part(PrimitiveType.Cube, sword, new Vector3(0f, 0f, 0.42f), new Vector3(0.015f, 0.06f, 0.66f), Mat(Color.Lerp(Metal, Color.white, 0.45f)));
            // 圓盾（左前臂外側）：防禦時轉到身體正前方
            shield = Pivot("Shield", elL, new Vector3(-0.07f, -0.14f, 0f));
            shield.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cylinder, shield, Vector3.zero, new Vector3(0.4f, 0.015f, 0.4f), Mat(Metal, 0.8f));
            Part(PrimitiveType.Cylinder, shield, Vector3.zero, new Vector3(0.34f, 0.025f, 0.34f), Mat(clsColor));
            Part(PrimitiveType.Sphere, shield, Vector3.zero, new Vector3(0.09f, 0.06f, 0.09f), Mat(Gold));
            shieldIdlePos = shield.localPosition; shieldIdleRot = shield.localRotation;

            baseR = new Arm(-10f, 8f, -40f, 0f); swingR = 0.6f;
            baseL = new Arm(-10f, 10f, -45f, 0f); swingL = 0.4f;
            guardL = new Arm(-70f, -25f, -60f, 0f); guardR = baseR;
            chargeR = new Arm(-150f, 10f, -30f, 0f);
            chargeL = new Arm(-30f, 10f, -50f, 0f);
            castR = new Arm(-45f, -10f, 0f, 45f);
        }

        // ---------------------------------------------------------------- 牧師：白袍金邊、主教冠、聖帶、太陽十字法杖、頭頂光環
        void BuildPriest()
        {
            var trim = Color.Lerp(Gold, clsColor, 0.35f);
            // 頭髮（淡金色，後腦＋兩側垂到耳下）
            var hairC = new Color(0.93f, 0.82f, 0.55f);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.01f, -0.045f), new Vector3(0.295f, 0.27f, 0.25f), Mat(hairC, 0.9f));
            for (int sd = -1; sd <= 1; sd += 2)
                Part(PrimitiveType.Capsule, head, new Vector3(0.12f * sd, -0.07f, -0.02f), new Vector3(0.06f, 0.09f, 0.08f), Mat(hairC, 0.85f));
            // 主教冠：前後壓扁的高圓台（往上收窄）＋金色底箍、正面直條與十字＋背後垂下的兩條飄帶
            var mitre = Pivot("Mitre", head, new Vector3(0f, 0.065f, -0.01f));
            mitre.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            var crown = MeshPart("MitreBody", mitre, Frustum(0.15f, 0.07f, 0.24f, 20), Vector3.zero, Mat(RobeWhite));
            crown.transform.localScale = new Vector3(1f, 1f, 0.72f);
            var band = MeshPart("MitreBand", mitre, Frustum(0.155f, 0.148f, 0.045f, 20), new Vector3(0f, -0.005f, 0f), Mat(trim, 0.9f));
            band.transform.localScale = new Vector3(1f, 1f, 0.74f);
            Part(PrimitiveType.Cube, mitre, new Vector3(0f, 0.12f, 0.086f), new Vector3(0.035f, 0.2f, 0.012f), Mat(trim), new Vector3(-13.6f, 0f, 0f));
            Part(PrimitiveType.Cube, mitre, new Vector3(0f, 0.15f, 0.082f), new Vector3(0.09f, 0.03f, 0.014f), Mat(Gold), new Vector3(-13.6f, 0f, 0f));
            Part(PrimitiveType.Sphere, mitre, new Vector3(0f, 0.25f, 0f), new Vector3(0.045f, 0.045f, 0.045f), Mat(Gold));
            for (int sd = -1; sd <= 1; sd += 2)
                Part(PrimitiveType.Cube, head, new Vector3(0.06f * sd, -0.1f, -0.16f), new Vector3(0.045f, 0.2f, 0.012f), Mat(trim, 0.8f), new Vector3(12f, 0f, 0f));
            // 長袍：從腰到腳踝的喇叭狀白袍＋金邊袍擺
            MeshPart("Robe", spine, Frustum(0.26f, 0.16f, 0.95f, 24), new Vector3(0f, -0.8f, 0f), Mat(RobeWhite, 0.85f));
            MeshPart("RobeHem", spine, Frustum(0.265f, 0.255f, 0.06f, 24), new Vector3(0f, -0.81f, 0f), Mat(trim, 0.75f));
            // 披肩（短斗篷）：圍住肩膀的圓台＋金色下緣
            MeshPart("Mantle", spine, Frustum(0.235f, 0.12f, 0.17f, 24), new Vector3(0f, 0.38f, 0f), Mat(RobeWhite));
            MeshPart("MantleHem", spine, Frustum(0.24f, 0.232f, 0.025f, 24), new Vector3(0f, 0.375f, 0f), Mat(trim, 0.85f));
            // 聖帶（stole）：從胸前沿著長袍正面垂到袍擺的兩條金帶，下端有小十字
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Part(PrimitiveType.Cube, spine, new Vector3(0.055f * sd, 0.3f, 0.112f), new Vector3(0.045f, 0.36f, 0.012f), Mat(trim, 0.9f));
                Part(PrimitiveType.Cube, spine, new Vector3(0.055f * sd, -0.36f, 0.215f), new Vector3(0.045f, 0.86f, 0.012f), Mat(trim, 0.8f), new Vector3(-6f, 0f, 0f));
                Part(PrimitiveType.Cube, spine, new Vector3(0.055f * sd, -0.66f, 0.25f), new Vector3(0.012f, 0.07f, 0.014f), Mat(RobeWhite), new Vector3(-6f, 0f, 0f));
                Part(PrimitiveType.Cube, spine, new Vector3(0.055f * sd, -0.65f, 0.249f), new Vector3(0.04f, 0.012f, 0.014f), Mat(RobeWhite), new Vector3(-6f, 0f, 0f));
            }
            // 寬袖口（白＋金邊）
            MeshPart("CuffL", elL, Frustum(0.08f, 0.05f, 0.1f, 12), new Vector3(0f, -0.25f, 0f), Mat(RobeWhite, 0.9f));
            MeshPart("CuffR", elR, Frustum(0.08f, 0.05f, 0.1f, 12), new Vector3(0f, -0.25f, 0f), Mat(RobeWhite, 0.9f));
            MeshPart("CuffTrimL", elL, Frustum(0.082f, 0.079f, 0.02f, 12), new Vector3(0f, -0.255f, 0f), Mat(trim, 0.85f));
            MeshPart("CuffTrimR", elR, Frustum(0.082f, 0.079f, 0.02f, 12), new Vector3(0f, -0.255f, 0f), Mat(trim, 0.85f));

            // 法杖（沿手的 +Z，前臂水平時直立）：象牙白杖身＋金色環節，頂端是太陽環＋十字，中心發光寶珠
            var staff = Pivot("Staff", handR, new Vector3(0f, -0.04f, 0f));
            Part(PrimitiveType.Cylinder, staff, new Vector3(0f, 0f, 0.2f), new Vector3(0.032f, 0.66f, 0.032f), Mat(new Color(0.93f, 0.9f, 0.82f), 0.85f), new Vector3(90f, 0f, 0f));
            foreach (float z in new[] { -0.3f, 0.02f, 0.78f })
                Part(PrimitiveType.Cylinder, staff, new Vector3(0f, 0f, z), new Vector3(0.042f, 0.015f, 0.042f), Mat(Gold, 0.9f), new Vector3(90f, 0f, 0f));
            MeshPart("StaffNeck", staff, Frustum(0.022f, 0.045f, 0.06f, 12), new Vector3(0f, 0f, 0.8f), Mat(Gold), new Vector3(90f, 0f, 0f));
            // 太陽環：在杖軸與身體左右方向構成的平面上（正面看得到整個圓）
            var sunC = new Vector3(0f, 0f, 1.0f);
            const float sunR = 0.11f;
            const int SN = 14;
            var goldMat = Mat(Gold);
            for (int i = 0; i < SN; i++)
            {
                float a0 = 2f * Mathf.PI * i / SN, a1 = 2f * Mathf.PI * (i + 1) / SN;
                Seg(staff, sunC + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * sunR, sunC + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * sunR, 0.022f, goldMat);
            }
            // 光芒：環外八道短尖刺
            for (int i = 0; i < 8; i++)
            {
                float a = 2f * Mathf.PI * (i + 0.5f) / 8f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var spike = Pivot("Ray", staff, sunC + d * (sunR + 0.008f));
                spike.localRotation = Quaternion.FromToRotation(Vector3.up, d);
                MeshPart("RayCone", spike, Frustum(0.014f, 0f, 0.055f, 6), Vector3.zero, Mat(Gold, 0.95f));
            }
            // 環內的十字
            Seg(staff, sunC + new Vector3(0f, 0f, -sunR), sunC + new Vector3(0f, 0f, sunR + 0.06f), 0.026f, goldMat);
            Seg(staff, sunC + new Vector3(-sunR, 0f, 0.015f), sunC + new Vector3(sunR, 0f, 0.015f), 0.026f, goldMat);
            orb = Part(PrimitiveType.Sphere, staff, sunC + new Vector3(0f, 0f, 0.015f), new Vector3(0.075f, 0.075f, 0.075f),
                Glow(Color.Lerp(clsColor, Color.white, 0.55f))).transform;
            orbBaseScale = orb.localScale;

            // 頭頂漂浮的光環（淡淡的發光圓環，動作裡慢慢上下浮動、旋轉）
            halo = Pivot("Halo", head, new Vector3(0f, 0.37f, -0.03f));
            halo.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            haloBasePos = halo.localPosition;
            MeshPart("HaloRing", halo, RingMesh(0.12f, 0.145f, 36), Vector3.zero, Glow(new Color(1f, 0.93f, 0.6f, 0.75f)));
            MeshPart("HaloSoft", halo, RingMesh(0.105f, 0.165f, 36), new Vector3(0f, -0.002f, 0f), Glow(new Color(clsColor.r, clsColor.g, clsColor.b, 0.22f)));

            // 姿勢：右手持杖；詠唱時高舉法杖、左手收在胸前祈禱
            baseR = new Arm(-10f, 6f, -80f, 0f); swingR = 0.35f;
            baseL = new Arm(-5f, 8f, -25f, 0f); swingL = 0.6f;
            chargeR = new Arm(-80f, 4f, -12f, 0f);
            chargeL = new Arm(-35f, -25f, -110f, 0f);
            castR = new Arm(-105f, 0f, -5f, 55f);
            legAmpScale = 0.55f;
        }

        // ================================================================ 對外操作
        public void SetPose(Vector3 feetPosition, Vector3 forward)
        {
            transform.position = feetPosition;
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        public void PlayCast() { if (!dead && !isDummy) castT = 0f; }

        public void PlayHit()
        {
            if (dead) return;
            hitT = HitDur;
            SetFlash(true);
            if (isDummy)
            {
                // 被打得往後晃，帶一點隨機的左右晃動
                wobVel += new Vector2(-170f, Random.Range(-70f, 70f));
            }
        }

        public void SetDead(bool d)
        {
            dead = d;
            if (d) { castT = -1f; }
        }

        public void SetVisible(bool v)
        {
            visible = v;
            if (rends == null) return;
            for (int i = 0; i < rends.Length; i++) if (rends[i]) rends[i].enabled = v;
            if (!v && aura) aura.SetActive(false);
        }

        void SetFlash(bool on)
        {
            if (flashing == on || rends == null) return;
            flashing = on;
            for (int i = 0; i < rends.Length; i++)
                if (rends[i])
                {
                    // Keep the facial ink/highlights readable during the body hit flash.
                    bool facePaint = head != null && rends[i].transform.IsChildOf(head) && origMats[i].shader == glowShader;
                    rends[i].sharedMaterial = on && !facePaint ? flashMat : origMats[i];
                }
        }

        // ================================================================ 動作
        void Update() { Animate(Time.deltaTime); }

        void Animate(float dt)
        {
            if (body == null) return;
            if (isDummy) { AnimateDummy(dt); return; }
            clock += dt;

            // 平滑的狀態量
            float speed = dead ? 0f : Mathf.Clamp(MoveSpeed, 0f, 3f);
            walkAmt = Mathf.MoveTowards(walkAmt, Mathf.Clamp01(speed / 0.8f), dt * 4f);
            if (speed > 0.01f) walkPhase += dt * Mathf.PI * 2f * Mathf.Max(speed, 0.6f) / 1.1f;
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 3f);
            if (walkPhase > 1000f) walkPhase -= Mathf.PI * 300f;
            guardAmt = Mathf.MoveTowards(guardAmt, Guarding && !dead ? 1f : 0f, dt * 8f);
            float ch = dead ? 0f : Mathf.Clamp01(Charge);
            float chargeTarget = (!dead && Charge > 0f) ? 1f : 0f;
            chargeAmt = Mathf.MoveTowards(chargeAmt, chargeTarget, dt * 6f);
            deadAmt = Mathf.MoveTowards(deadAmt, dead ? 1f : 0f, dt / DieDur);
            float castK = 0f;
            if (castT >= 0f)
            {
                castT += dt;
                if (castT >= CastDur) castT = -1f;
                else castK = Mathf.Sin(Mathf.PI * castT / CastDur);
            }
            float hitLean = 0f;
            if (hitT > 0f)
            {
                hitT -= dt;
                hitLean = Mathf.Clamp01(hitT / HitDur);
                if (hitT <= 0f) SetFlash(false);
            }

            float sinP = Mathf.Sin(walkPhase), cosP = Mathf.Cos(walkPhase);
            float amp = 32f * walkAmt * legAmpScale;
            float breath = Mathf.Sin(clock * 2.2f);
            float deadE = deadAmt * deadAmt * (3f - 2f * deadAmt);   // smoothstep

            // ---- 身體：走路起伏、呼吸、倒地
            float bob = walkAmt * Mathf.Abs(cosP) * 0.035f + (1f - walkAmt) * breath * 0.004f;
            body.localPosition = new Vector3(0f, bob * (1f - deadE) + 0.11f * deadE, 0f);
            body.localRotation = Quaternion.Euler(-88f * deadE, 0f, 0f);

            // ---- 腿
            float kneeAmp = amp * 1.3f;
            hipL.localRotation = Quaternion.Euler(sinP * amp * (1f - deadE), 0f, 0f);
            hipR.localRotation = Quaternion.Euler(-sinP * amp * (1f - deadE), 0f, 0f);
            knL.localRotation = Quaternion.Euler(Mathf.Max(0f, cosP) * kneeAmp + 3f, 0f, 0f);
            knR.localRotation = Quaternion.Euler(Mathf.Max(0f, -cosP) * kneeAmp + 3f, 0f, 0f);

            // ---- 軀幹、頭：走路前傾、詠唱挺胸、受擊後仰、呼吸
            float lean = walkAmt * 7f - hitLean * 16f - chargeAmt * 3f + breath * 0.8f * (1f - walkAmt);
            spine.localRotation = Quaternion.Euler(lean * (1f - deadE), Mathf.Sin(walkPhase) * 4f * walkAmt, 0f);
            neck.localRotation = Quaternion.Euler(-lean * 0.5f + Mathf.Sin(clock * 0.7f) * 2f, Mathf.Sin(clock * 0.43f) * 6f * (1f - walkAmt), 0f);

            // ---- 手臂：基本姿勢 → 加走路擺動 → 防禦 → 詠唱 → 出手 → 倒地
            float armSwing = sinP * amp * 0.8f;
            var aL = baseL; aL.x -= armSwing * swingL;
            var aR = baseR; aR.x += armSwing * swingR;
            aL.zOut += breath * 1.5f; aR.zOut += breath * 1.5f;
            aL = Arm.Lerp(aL, guardL, guardAmt); aR = Arm.Lerp(aR, guardR, guardAmt);
            aL = Arm.Lerp(aL, chargeL, chargeAmt); aR = Arm.Lerp(aR, chargeR, chargeAmt);
            if (chargeAmt > 0f)
            {
                // 蓄力時手微微顫動
                float tremble = Mathf.Sin(clock * 37f) * 1.5f * ch;
                aL.x += tremble; aR.x -= tremble;
            }
            aL = Arm.Lerp(aL, castL, castK * castMaskL); aR = Arm.Lerp(aR, castR, castK * castMaskR);
            aL = Arm.Lerp(aL, Relaxed, deadE); aR = Arm.Lerp(aR, Relaxed, deadE);
            ApplyArm(shL, elL, handL, aL, -1f);
            ApplyArm(shR, elR, handR, aR, 1f);

            // ---- 盾：防禦時轉到正前方
            if (shield != null)
            {
                float g = guardAmt * (1f - deadE);
                Vector3 idlePos = elL.TransformPoint(shieldIdlePos);
                Quaternion idleRot = elL.rotation * shieldIdleRot;
                Vector3 guardPos = body.TransformPoint(new Vector3(-0.03f, 1.2f, 0.32f));
                Quaternion guardRot = body.rotation * Quaternion.Euler(90f, 0f, 0f);
                shield.position = Vector3.Lerp(idlePos, guardPos, g);
                shield.rotation = Quaternion.Slerp(idleRot, guardRot, g);
            }

            // ---- 披風、圍巾：走路時往後飄
            float flutter = Mathf.Sin(clock * 5f + walkPhase) * (2f + 4f * walkAmt);
            if (cape != null) cape.localRotation = Quaternion.Euler(4f + walkAmt * 28f + flutter, 0f, 0f);
            if (scarfTail != null) scarfTail.localRotation = Quaternion.Euler(15f + walkAmt * 45f + flutter * 1.5f, 0f, Mathf.Sin(clock * 3.1f) * 8f);

            // ---- 法杖寶珠：詠唱時變亮變大
            if (orb != null)
            {
                float s = 1f + chargeAmt * (0.4f + 0.6f * ch) + Mathf.Sin(clock * (4f + 10f * ch)) * (0.05f + 0.12f * chargeAmt) + castK * 0.5f;
                orb.localScale = orbBaseScale * s;
            }

            // ---- 牧師頭頂光環：緩緩上下浮動、旋轉；詠唱時變大
            if (halo != null)
            {
                halo.localPosition = haloBasePos + new Vector3(0f, Mathf.Sin(clock * 1.8f) * 0.012f + chargeAmt * 0.03f, 0f);
                halo.localRotation = Quaternion.Euler(-10f * (1f - chargeAmt), clock * 25f, 0f);
                halo.localScale = Vector3.one * (1f + chargeAmt * (0.15f + 0.15f * ch) + castK * 0.25f);
            }

            // ---- 腳下光圈
            if (aura != null)
            {
                bool show = visible && !dead && Charge > 0f;
                if (aura.activeSelf != show) aura.SetActive(show);
                if (show)
                {
                    float pulse = Mathf.Sin(clock * (5f + 8f * ch));
                    float sc = 0.85f + 0.35f * ch + pulse * 0.05f;
                    aura.transform.localScale = new Vector3(sc, 1f, sc);
                    aura.transform.localRotation = Quaternion.Euler(0f, clock * 60f, 0f);
                    auraMat.color = new Color(clsColor.r, clsColor.g, clsColor.b, 0.35f + 0.45f * ch + pulse * 0.1f);
                }
            }
            AnimateDetails(castK, hitLean);
        }

        // ================================================================ 木頭人（練習靶）
        public const string DummyId = "dummy";
        static readonly Color DummyWood = new Color(0.76f, 0.43f, 0.18f);
        static readonly Color DummyDark = new Color(0.53f, 0.28f, 0.12f);
        static readonly Color Straw = new Color(1f, 0.8f, 0.3f);
        static readonly Color Burlap = new Color(0.86f, 0.74f, 0.52f);
        static readonly Color Rope = new Color(0.76f, 0.52f, 0.22f);
        static readonly Color TargetRed = new Color(0.96f, 0.16f, 0.13f);
        static readonly Color TargetWhite = new Color(0.97f, 0.94f, 0.86f);

        bool isDummy;
        Transform post;               // 搖晃的樞紐（底座上方）
        Vector2 wob, wobVel;          // 搖晃角度（x = 前後、y = 左右，度）與角速度
        static readonly Vector3 DummyFallPivot = new Vector3(0f, 0f, -0.22f);   // 往後倒時繞底座後緣翻

        /// <summary>
        /// 木頭人：十字底座＋木樁、稻草束成的軀幹（三道麻繩）、胸前紅白靶心、
        /// 左右斜伸的木樁手與前方一支木樁、麻布頭（畫上眼睛與紅色縫線嘴）＋頭頂稻草髻。總高約 1.7 m。
        /// </summary>
        void BuildDummy()
        {
            isDummy = true;
            clsColor = Straw;
            body = Pivot("Body", transform, Vector3.zero);

            // ---- 底座：十字木腳＋圓木墩（不跟著搖晃）
            Part(PrimitiveType.Cube, body, new Vector3(0f, 0.03f, 0f), new Vector3(0.5f, 0.06f, 0.09f), Mat(DummyDark, 0.75f));
            Part(PrimitiveType.Cube, body, new Vector3(0f, 0.03f, 0f), new Vector3(0.09f, 0.06f, 0.5f), Mat(DummyDark, 0.75f));
            Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.07f, 0f), new Vector3(0.24f, 0.05f, 0.24f), Mat(DummyWood, 0.8f));

            // ---- 搖晃的部分（樞紐在底座上方）
            const float py = 0.1f;
            post = Pivot("Post", body, new Vector3(0f, py, 0f));
            Vector3 P(float x, float y, float z) => new Vector3(x, y - py, z);

            // 木樁（從底座一直到脖子）
            Part(PrimitiveType.Cylinder, post, P(0f, 0.75f, 0f), new Vector3(0.1f, 0.68f, 0.1f), Mat(DummyWood, 0.85f));
            Part(PrimitiveType.Cylinder, post, P(0f, 0.16f, 0f), new Vector3(0.13f, 0.02f, 0.13f), Mat(Rope, 0.85f));

            // 稻草軀幹＋三道麻繩
            Part(PrimitiveType.Capsule, post, P(0f, 1.0f, 0f), new Vector3(0.34f, 0.33f, 0.3f), Mat(Straw));
            MeshPart("StrawSkirt", post, Frustum(0.19f, 0.13f, 0.14f, 16), P(0f, 0.62f, 0f), Mat(Mul(Straw, 0.9f), 0.8f));
            foreach (float y in new[] { 0.8f, 1.02f, 1.22f })
            {
                float rx = y > 1.15f ? 0.33f : 0.355f, rz = y > 1.15f ? 0.29f : 0.315f;
                Part(PrimitiveType.Cylinder, post, P(0f, y, 0f), new Vector3(rx, 0.014f, rz), Mat(Rope, 0.85f));
            }

            // 胸前靶心（木板＋紅白同心圓）
            var tgt = Pivot("Target", post, P(0f, 1.06f, 0.15f));
            Part(PrimitiveType.Cylinder, tgt, Vector3.zero, new Vector3(0.24f, 0.012f, 0.24f), Mat(DummyWood), new Vector3(90f, 0f, 0f));
            float[] rr = { 0.2f, 0.15f, 0.1f, 0.05f };
            for (int i = 0; i < rr.Length; i++)
                Part(PrimitiveType.Cylinder, tgt, new Vector3(0f, 0f, 0.008f + i * 0.003f), new Vector3(rr[i], 0.004f, rr[i]),
                    Mat(i % 2 == 0 ? TargetWhite : TargetRed), new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, tgt, new Vector3(0f, 0f, 0.022f), new Vector3(0.025f, 0.004f, 0.025f), Mat(TargetRed), new Vector3(90f, 0f, 0f));

            // 木樁手：左右上方各一支往前斜伸，正前方下方一支（詠春木人樁）
            var pegMat = Mat(DummyWood, 0.9f);
            var knobMat = Mat(DummyDark, 0.85f);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 a = P(0.12f * sd, 1.22f, 0.04f), b = P(0.27f * sd, 1.27f, 0.17f);
                Seg(post, a, b, 0.055f, pegMat);
                Part(PrimitiveType.Sphere, post, b, new Vector3(0.065f, 0.065f, 0.065f), knobMat);
            }
            Seg(post, P(0f, 0.9f, 0.1f), P(0f, 0.86f, 0.27f), 0.055f, pegMat);
            Part(PrimitiveType.Sphere, post, P(0f, 0.86f, 0.27f), new Vector3(0.065f, 0.065f, 0.065f), knobMat);

            // 脖子（麻繩綁住）
            Part(PrimitiveType.Cylinder, post, P(0f, 1.38f, 0f), new Vector3(0.09f, 0.05f, 0.09f), Mat(DummyWood, 0.8f));
            Part(PrimitiveType.Cylinder, post, P(0f, 1.36f, 0f), new Vector3(0.11f, 0.012f, 0.11f), Mat(Rope, 0.8f));

            // 頭：麻布球＋畫上的眼睛、紅色縫線嘴、頭頂稻草髻
            var hd = Pivot("Head", post, P(0f, 1.53f, 0f));
            head = hd;
            Part(PrimitiveType.Sphere, hd, Vector3.zero, new Vector3(0.27f, 0.28f, 0.26f), Mat(Burlap));
            for (int sd = -1; sd <= 1; sd += 2)
                Part(PrimitiveType.Sphere, hd, new Vector3(0.05f * sd, 0.025f, 0.122f), new Vector3(0.035f, 0.04f, 0.012f), Mat(EyeCol));
            Part(PrimitiveType.Cube, hd, new Vector3(0f, -0.05f, 0.122f), new Vector3(0.09f, 0.012f, 0.01f), Mat(TargetRed));
            for (int i = -1; i <= 1; i++)
                Part(PrimitiveType.Cube, hd, new Vector3(0.03f * i, -0.05f, 0.124f), new Vector3(0.008f, 0.035f, 0.01f), Mat(TargetRed));
            Part(PrimitiveType.Cylinder, hd, new Vector3(0f, 0.125f, 0f), new Vector3(0.08f, 0.012f, 0.08f), Mat(Rope, 0.85f));
            MeshPart("Topknot", hd, Frustum(0.045f, 0.012f, 0.06f, 10), new Vector3(0f, 0.13f, 0f), Mat(Straw, 0.95f));

            // 受擊閃色用：記住每個部位原本的材質
            flashMat = Glow(new Color(1f, 0.55f, 0.5f));
            rends = body.GetComponentsInChildren<Renderer>(true);
            origMats = new Material[rends.Length];
            for (int i = 0; i < rends.Length; i++) origMats[i] = rends[i].sharedMaterial;

            AnimateDummy(0f);
        }

        /// <summary>木頭人動作：受擊時彈簧搖晃（約 0.8 秒衰減）＋閃色；倒地時往後翻倒，SetDead(false) 再立起來。</summary>
        void AnimateDummy(float dt)
        {
            clock += dt;
            float h = Mathf.Min(dt, 0.05f);

            // 彈簧搖晃：自然頻率約 2.5 Hz，阻尼讓振幅約 0.8 秒衰減到 5%
            const float K = 247f, C = 7.5f;
            var acc = -K * wob - C * wobVel;
            wobVel += acc * h;
            wob += wobVel * h;
            wob.x = Mathf.Clamp(wob.x, -25f, 25f); wob.y = Mathf.Clamp(wob.y, -25f, 25f);
            post.localRotation = Quaternion.Euler(wob.x, 0f, wob.y);

            if (hitT > 0f)
            {
                hitT -= dt;
                if (hitT <= 0f) SetFlash(false);
            }

            // 倒下：像重力一樣越倒越快；站起來：平順地立回去
            deadAmt = Mathf.MoveTowards(deadAmt, dead ? 1f : 0f, dt / (dead ? DieDur : 0.5f));
            float e = dead ? deadAmt * deadAmt : deadAmt * deadAmt * (3f - 2f * deadAmt);
            var q = Quaternion.Euler(-88f * e, 0f, 0f);
            body.localRotation = q;
            body.localPosition = DummyFallPivot - q * DummyFallPivot;
        }

        static void ApplyArm(Transform sh, Transform el, Transform hand, Arm a, float side)
        {
            sh.localRotation = Quaternion.Euler(a.x, 0f, a.zOut * side);
            el.localRotation = Quaternion.Euler(a.el, 0f, 0f);
            hand.localRotation = Quaternion.Euler(a.wr, 0f, 0f);
        }

        void OnDestroy()
        {
            if (auraMat != null) Destroy(auraMat);
        }

        // ================================================================ 建構小工具
        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static Mesh smoothCube;

        // Keep the box silhouette and dimensions, but give coincident corners the same
        // normal so the inverted hull stays continuous. Never modify Unity's shared mesh.
        internal static Mesh SmoothCube(Mesh source)
        {
            if (smoothCube != null) return smoothCube;
            var vertices = source.vertices;
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) normals[i] = vertices[i].normalized;
            smoothCube = new Mesh
            {
                name = "Rig Smooth Cube", vertices = vertices,
                normals = normals, uv = source.uv, triangles = source.triangles
            };
            smoothCube.RecalculateBounds();
            return smoothCube;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default)
        {
            var mesh = DetailPrimitive(type);
            GameObject go;
            if (mesh != null)
            {
                go = new GameObject(type.ToString());
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
            }
            else
            {
                go = GameObject.CreatePrimitive(type);
                if (type == PrimitiveType.Cube && !unlit && mat.shader == litShader)
                {
                    var filter = go.GetComponent<MeshFilter>(); filter.sharedMesh = SmoothCube(filter.sharedMesh);
                }
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                }
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        static GameObject MeshPart(string name, Transform parent, Mesh mesh, Vector3 localPos, Material mat, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>從 a 到 b 的細圓柱（本地座標）。</summary>
        static void Seg(Transform parent, Vector3 a, Vector3 b, float width, Material mat)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            var go = Part(PrimitiveType.Cylinder, parent, (a + b) * 0.5f, new Vector3(width, len * 0.5f, width), mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
        }

        /// <summary>圓台（r1 = 0 時是圓錐），底面在 y = 0、往 +Y 長 h，上下都封口。結果依尺寸快取。</summary>
        static Mesh Frustum(float r0, float r1, float h, int seg)
        {
            string key = $"F{r0:F3}_{r1:F3}_{h:F3}_{seg}";
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            int ring = seg + 1;
            var v = new Vector3[ring * 2 + (ring + 1) * 2];
            var n = new Vector3[v.Length];
            var tris = new int[seg * 6 + seg * 3 * 2];
            float slope = (r0 - r1) / h;
            for (int i = 0; i < ring; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                var nn = new Vector3(c, slope, s).normalized;
                v[i] = new Vector3(c * r0, 0f, s * r0); n[i] = nn;
                v[ring + i] = new Vector3(c * r1, h, s * r1); n[ring + i] = nn;
            }
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int b0 = i, b1 = i + 1, t0 = ring + i, t1 = ring + i + 1;
                tris[t++] = t0; tris[t++] = t1; tris[t++] = b1;
                tris[t++] = t0; tris[t++] = b1; tris[t++] = b0;
            }
            // 底面（朝下）
            int bc = ring * 2;
            v[bc] = Vector3.zero; n[bc] = Vector3.down;
            for (int i = 0; i < ring; i++) { v[bc + 1 + i] = v[i]; n[bc + 1 + i] = Vector3.down; }
            for (int i = 0; i < seg; i++) { tris[t++] = bc; tris[t++] = bc + 1 + i; tris[t++] = bc + 2 + i; }
            // 頂面（朝上）
            int tc = bc + ring + 1;
            v[tc] = new Vector3(0f, h, 0f); n[tc] = Vector3.up;
            for (int i = 0; i < ring; i++) { v[tc + 1 + i] = v[ring + i]; n[tc + 1 + i] = Vector3.up; }
            for (int i = 0; i < seg; i++) { tris[t++] = tc; tris[t++] = tc + 2 + i; tris[t++] = tc + 1 + i; }

            var m = new Mesh { name = key, vertices = v, normals = n, triangles = tris };
            m.RecalculateBounds();
            meshCache[key] = m;
            return m;
        }

        /// <summary>水平圓環（XZ 平面），用在腳下的詠唱光圈。</summary>
        static Mesh RingMesh(float rIn, float rOut, int seg)
        {
            string key = $"R{rIn:F3}_{rOut:F3}_{seg}";
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            int ring = seg + 1;
            var v = new Vector3[ring * 2];
            var n = new Vector3[v.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i < ring; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i] = new Vector3(c * rIn, 0f, s * rIn); n[i] = Vector3.up;
                v[ring + i] = new Vector3(c * rOut, 0f, s * rOut); n[ring + i] = Vector3.up;
            }
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int i0 = i, i1 = i + 1, o0 = ring + i, o1 = ring + i + 1;
                tris[t++] = i0; tris[t++] = i1; tris[t++] = o1;
                tris[t++] = i0; tris[t++] = o1; tris[t++] = o0;
            }
            var m = new Mesh { name = key, vertices = v, normals = n, triangles = tris };
            m.RecalculateBounds();
            meshCache[key] = m;
            return m;
        }
    }
}
