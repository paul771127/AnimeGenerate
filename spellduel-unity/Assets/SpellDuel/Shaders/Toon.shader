// 賽璐珞（卡通）著色：明暗分成兩階（加一點柔邊）、邊緣亮光、外框黑邊（背面擠出法，粗細以螢幕像素計）。
// 風格參數是全域的（ToonStyle.cs 用 Shader.SetGlobal 設定），每個材質只需要 _Color／_MainTex。
Shader "SpellDuel/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;
            // 全域風格參數（ToonStyle）
            fixed4 _SD_ShadeColor;      // 暗部顏色倍率（偏藍紫＝動畫感）
            half _SD_ShadeThreshold;    // 亮暗分界（N·L）
            half _SD_ShadeSoftness;     // 分界柔邊寬度
            fixed4 _SD_RimColor;        // 邊緣亮光（a＝強度）
            half _SD_Ambient;           // 環境光量

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                float3 v : TEXCOORD2;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.v = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseC = tex2D(_MainTex, i.uv) * _Color;
                float3 n = normalize(i.n);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = dot(n, L);
                float soft = max(0.001, _SD_ShadeSoftness);
                float lit = smoothstep(_SD_ShadeThreshold - soft, _SD_ShadeThreshold + soft, ndl);
                fixed3 shade = baseC.rgb * _SD_ShadeColor.rgb;
                fixed3 c = lerp(shade, baseC.rgb, lit);
                c *= lerp(fixed3(1,1,1), _LightColor0.rgb, 0.35);
                c += baseC.rgb * UNITY_LIGHTMODEL_AMBIENT.rgb * _SD_Ambient;
                // 邊緣亮光：只在亮部、硬邊
                float rim = 1.0 - saturate(dot(n, normalize(i.v)));
                c += _SD_RimColor.rgb * _SD_RimColor.a * step(0.72, rim) * lit;
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SD_OutlineColor;
            float _SD_OutlineWidth;     // 以 1000 像素高的畫面為準的寬度（像素），實際依螢幕高度等比放大
            float _SD_OutlineFar;       // 超過這個距離（公尺）開始變細

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 p = UnityObjectToClipPos(v.vertex);
                float3 nv = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 dir = TransformViewToProjection(nv.xy);
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0, 0);
                float px = _SD_OutlineWidth * (_ScreenParams.y / 1000.0);
                float fade = saturate(_SD_OutlineFar / max(0.01, p.w));
                p.xy += dir * (px * 2.0 / _ScreenParams.xy) * p.w * fade;
                o.pos = p;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _SD_OutlineColor; }
            ENDCG
        }
    }
    Fallback "Legacy Shaders/Diffuse"
}
