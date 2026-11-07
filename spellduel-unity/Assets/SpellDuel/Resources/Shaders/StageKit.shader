Shader "SpellDuel/StageKit"
{
 // 生成器的 3D 地形零件（Resources/SceneKit）：光影已經烘在顏色圖裡 → 不打光（Unlit），只加發光遮罩和遠處的霧色。
 // 同一個材質、同一個網格的零件會用 GPU instancing 合併繪製（不會變慢）。
 Properties { _MainTex("Albedo",2D)="white"{} _EmissionMap("Emission mask",2D)="black"{} _EmissionColor("Emission",Color)=(1,0.45,0.12,1) _Glow("Glow",Float)=1.6 _Haze("Haze",Color)=(0.32,0.2,0.18,1) _HazeNear("Haze near",Float)=16 _HazeFar("Haze far",Float)=46 _HazeMax("Haze max",Float)=0.6 }
 SubShader {
 Tags { "Queue"="Geometry" "RenderType"="Opaque" }
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 sampler2D _MainTex,_EmissionMap;fixed4 _EmissionColor,_Haze;float _Glow,_HazeNear,_HazeFar,_HazeMax;
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float haze:TEXCOORD1;};
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);o.uv=v.uv;o.pos=UnityObjectToClipPos(v.vertex);
 float3 wp=mul(unity_ObjectToWorld,v.vertex).xyz;o.haze=smoothstep(_HazeNear,_HazeFar,length(wp-_WorldSpaceCameraPos))*_HazeMax;return o;}
 fixed4 frag(v2f i):SV_Target{fixed3 c=tex2D(_MainTex,i.uv).rgb;fixed g=tex2D(_EmissionMap,i.uv).r;c+=_EmissionColor.rgb*g*_Glow;c=lerp(c,_Haze.rgb,i.haze);return fixed4(c,1);}
 ENDCG }
 }
}
