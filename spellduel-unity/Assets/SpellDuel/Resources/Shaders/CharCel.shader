Shader "SpellDuel/CharCel"
{
    Properties
    {
        _MainTex ("Color", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadeColor ("Shade tint", Color) = (0.68,0.70,0.80,1)
        _Threshold ("Light threshold", Range(-1,1)) = 0.35
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            sampler2D _MainTex, _EmissionMap;
            float4 _MainTex_ST;
            fixed4 _Color, _ShadeColor;
            half _Threshold, _Cutoff;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*_Color;
                clip(c.a-_Cutoff);
                half light=step(_Threshold,dot(normalize(i.normal),normalize(UnityWorldSpaceLightDir(i.world))));
                if(i.uv.x<0.5 && i.uv.y>0.5) light=max(light,0.65);
                c.rgb*=lerp(_ShadeColor.rgb,fixed3(1,1,1),light);
                c.rgb+=tex2D(_EmissionMap,i.uv).rgb*1.5;
                return fixed4(c.rgb,1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
