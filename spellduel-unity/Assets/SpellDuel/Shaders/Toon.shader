// 賽璐珞（卡通）著色：明暗分成兩階（加一點柔邊）、邊緣亮光、外框黑邊（背面擠出法，粗細以螢幕像素計）。
// 一般物件仍用 ToonStyle 全域參數；只有人類材質開啟 SD_HUMAN，額外讀取臉部參數與頂點資料。
Shader "SpellDuel/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _ArtFade ("Visual opacity", Float) = 1
        [HideInInspector] _ArtGray ("Downed desaturation", Float) = 0
        [HideInInspector] _ArtRevive ("Revive light", Float) = 0
        [HideInInspector] _HumanFace ("Human face mode", Float) = 0
        [HideInInspector] _HumanHair ("Human hair", Float) = 0
        [HideInInspector] _HumanPart ("Hair part", Float) = 0
        [HideInInspector] _HumanWave ("Hair phase", Float) = 0
        [HideInInspector] _HumanFringe ("Hair shadow edge", Float) = 0.71
        [HideInInspector] _HumanOutline ("Human outline scale", Float) = 1
        [HideInInspector] _HumanStrandLines ("Player strand ink", Float) = 0
        [HideInInspector] _HumanSkinShade ("Soft pink skin shade", Color) = (0.93,0.85,0.85,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        // ---- 本體：兩階明暗
        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            // Metal、GLES3 與 Vulkan 的共同基線，容納解析式臉部遮罩。
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ SD_HUMAN
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; half _ArtFade, _ArtGray, _ArtRevive;
            #ifdef SD_HUMAN
            half _HumanFace;
            half _HumanHair, _HumanPart, _HumanWave, _HumanFringe;
            fixed4 _HumanSkinShade;
            #endif
            // 全域風格參數（ToonStyle）
            fixed4 _SD_ShadeColor;      // 暗部顏色倍率（偏藍紫＝動畫感）
            half _SD_ShadeThreshold;    // 亮暗分界（N·L）
            half _SD_ShadeSoftness;     // 分界柔邊寬度
            fixed4 _SD_RimColor;        // 邊緣亮光（a＝強度）
            half _SD_Ambient;           // 環境光量
            fixed4 _SD_EnvTint;         // AR 光照估計（ArLighting；預設白色＝不變）

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                float3 v : TEXCOORD2;
                #ifdef SD_HUMAN
                float3 faceRight : TEXCOORD3;
                float3 faceFront : TEXCOORD4;
                fixed4 data : COLOR;
                #endif
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.v = WorldSpaceViewDir(v.vertex);
                #ifdef SD_HUMAN
                o.faceRight = UnityObjectToWorldDir(float3(1,0,0));
                o.faceFront = UnityObjectToWorldDir(float3(0,0,1));
                o.data = v.color;
                #endif
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_ArtFade < 0.999) clip(_ArtFade - 0.001 - frac(dot(floor(i.pos.xy),float2(0.754877,0.56984)))*0.999);
                fixed4 baseC = tex2D(_MainTex, i.uv) * _Color;
                #ifdef SD_HUMAN
                if (_HumanHair > 0.5) baseC.rgb *= i.data.rgb;
                #endif
                float3 n = normalize(i.n);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = dot(n, L);
                float soft = max(0.001, _SD_ShadeSoftness);
                float lit = smoothstep(_SD_ShadeThreshold - soft, _SD_ShadeThreshold + soft, ndl);
                fixed3 shade = baseC.rgb * _SD_ShadeColor.rgb;
                #ifdef SD_HUMAN
                // 只處理人類材質；原始變體仍使用原本 N·L 與暗部。
                if (_HumanHair > 0.5) shade = baseC.rgb * (i.data.a < 0.25 ? _HumanSkinShade.rgb : fixed3(0.52,0.48,0.55));
                if (_HumanFace > 0.5)
                {
                    shade = baseC.rgb * _HumanSkinShade.rgb;
                    if (_HumanFace > 4.5) lit = 1.0 - 0.45*smoothstep(0.45,1.0,i.uv.y);
                    else if (_HumanFace < 1.5)
                    {
                        float front = dot(normalize(i.faceFront), L);
                        float side = dot(normalize(i.faceRight), L);
                        float horizontal = max(0.001, length(float2(front,side)));
                        // Same constants/equations as HumanFaceLight.Occlusion and the preview.
                        float sideGate = 1.0 - smoothstep(0.22,0.50,front/horizontal);
                        float x = (i.uv.x-0.5)*(side < 0 ? 2.0 : -2.0);
                        float curve = 0.20 + 0.80*(i.uv.y-0.46)*(i.uv.y-0.46);
                        float cheek = smoothstep(curve-0.20,curve+0.20,x)*sideGate;
                        float2 noseUV = (i.uv-float2(0.498,0.267))/float2(0.028,0.012);
                        float nose = 1.0-smoothstep(0.35,1.0,dot(noseUV,noseUV));
                        float edge = _HumanFringe + 0.028*sin(i.uv.x*31.0+_HumanWave)
                            + 0.020*sin(i.uv.x*15.0+_HumanPart*4.0);
                        float fringe = smoothstep(edge-0.02,edge+0.06,i.uv.y)*(1.0-smoothstep(0.87,0.94,i.uv.y));
                        float chin = (1.0-smoothstep(0.0,0.06,i.uv.y))*0.45;
                        float occlusion = max(max(cheek,nose*0.24),max(fringe*0.32,chin));
                        // Do not blend back to the sphere's N·L at the edge of the face.
                        lit = 1.0-occlusion;
                        shade = baseC.rgb*fixed3(0.93,0.85,0.85);
                    }
                }
                #endif
                fixed3 c = lerp(shade, baseC.rgb, lit);
                c *= lerp(fixed3(1,1,1), _LightColor0.rgb, 0.35);
                c += baseC.rgb * UNITY_LIGHTMODEL_AMBIENT.rgb * _SD_Ambient;
                // 邊緣亮光：只在亮部、硬邊
                float rim = 1.0 - saturate(dot(n, normalize(i.v)));
                c += _SD_RimColor.rgb * _SD_RimColor.a * step(0.72, rim) * lit;
                #ifdef SD_HUMAN
                // Vertex alpha: 0 skin, 0.5 luminous garment rune, 1 cloth/hair. No extra pass.
                if (_HumanHair > 0.5 && i.data.a > 0.4 && i.data.a < 0.6) c = baseC.rgb;
                #endif
                c *= _SD_EnvTint.rgb;   // AR 光照估計（ArLighting）：房間亮度與色溫
                c = lerp(c,dot(c,fixed3(0.299,0.587,0.114)).xxx,_ArtGray);
                c += fixed3(0.28,1.0,0.65)*_ArtRevive;
                return fixed4(c, baseC.a);
            }
            ENDCG
        }

        // ---- 外框：把背面沿法線往外推（螢幕上固定像素寬，遠的東西會稍微變細）
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            CGPROGRAM
            // Metal、GLES3 與 Vulkan 的共同基線，容納解析式臉部遮罩。
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ SD_HUMAN
            #include "UnityCG.cginc"

            fixed4 _SD_OutlineColor;
            float _SD_OutlineWidth;     // 以 1000 像素高的畫面為準的寬度（像素），實際依螢幕高度等比放大
            float _SD_OutlineFar;       // 超過這個距離（公尺）開始變細

            half _ArtFade;
            #ifdef SD_HUMAN
            fixed4 _Color;
            float _HumanOutline;
            half _HumanFace, _HumanHair, _HumanStrandLines;
            #endif
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata_full v)
            {
                v2f o;
                float4 p = UnityObjectToClipPos(v.vertex);
                float3 nv = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 dir = TransformViewToProjection(nv.xy);
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0, 0);
                float px = _SD_OutlineWidth * (_ScreenParams.y / 1000.0);
                #ifdef SD_HUMAN
                px *= _HumanOutline * (_HumanFace > 0.5 && _HumanFace < 1.5 ? v.color.r : 1.0) * (_HumanStrandLines > 0.5 && v.color.a < 0.9 ? 0.55 : 1.0);
                if (_HumanHair > 0.5 && v.color.a > 0.4 && v.color.a < 0.6) px *= 0.25;
                #endif
                float fade = saturate(_SD_OutlineFar / max(0.01, p.w));
                p.xy += dir * (px * 2.0 / _ScreenParams.xy) * p.w * fade;
                o.pos = p;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_ArtFade < 0.999) clip(_ArtFade - 0.001 - frac(dot(floor(i.pos.xy),float2(0.754877,0.56984)))*0.999);
                #ifdef SD_HUMAN
                fixed3 ink = _HumanHair > 0.5 ? fixed3(0.065,0.050,0.080) : _Color.rgb * fixed3(0.25,0.20,0.25);
                return fixed4(ink, 1);
                #else
                return _SD_OutlineColor;
                #endif
            }
            ENDCG
        }
    }
    Fallback "Legacy Shaders/Diffuse"
}
