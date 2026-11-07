Shader "SpellDuel/StageKit"
{
 // 生成器的 3D 地形零件（Resources/SceneKit）：光影已經烘在顏色圖裡 → 不打光（Unlit），只加熔岩感和遠處的霧色。
 //   ・發光遮罩（_EmissionMap）：裂縫的熔岩光，會慢慢脈動（每個位置的節奏不一樣）。
 //   ・牆腳的熔岩反光（_UnderGlow）：離地面越近越亮的暖橘色，像地上的熔岩照上來；偶爾有幾段比較亮。
 //   ・遠處淡成霧色。
 // 同一個材質、同一個網格的零件會用 GPU instancing 合併繪製（不會變慢）。
 Properties { _MainTex("Albedo",2D)="white"{} _EmissionMap("Emission mask",2D)="black"{} _EmissionColor("Emission",Color)=(1,0.45,0.12,1) _Glow("Glow",Float)=1.6
  _UnderGlow("Lava underglow",Color)=(1,0.38,0.1,1) _UnderGlowHeight("Underglow height (m)",Float)=1.6 _UnderGlowStrength("Underglow strength",Float)=0
  _Haze("Haze",Color)=(0.32,0.2,0.18,1) _HazeNear("Haze near",Float)=16 _HazeFar("Haze far",Float)=46 _HazeMax("Haze max",Float)=0.6 }
 SubShader {
 Tags { "Queue"="Geometry" "RenderType"="Opaque" }
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 sampler2D _MainTex,_EmissionMap;fixed4 _EmissionColor,_Haze,_UnderGlow;float _Glow,_HazeNear,_HazeFar,_HazeMax,_UnderGlowHeight,_UnderGlowStrength;
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float haze:TEXCOORD1;float under:TEXCOORD2;float pulse:TEXCOORD3;};
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);o.uv=v.uv;o.pos=UnityObjectToClipPos(v.vertex);
  float3 wp=mul(unity_ObjectToWorld,v.vertex).xyz;
  o.haze=smoothstep(_HazeNear,_HazeFar,length(wp-_WorldSpaceCameraPos))*_HazeMax;
  float h=max(wp.y-unity_ObjectToWorld._m13,0)/_UnderGlowHeight;   // 零件的原點在地面：離原點的高度＝離地高度（FBX 裡有子物件旋轉也對）
  float band=0.55+0.45*sin(wp.x*0.37+wp.z*0.29)*sin(wp.x*0.11-wp.z*0.17+_Time.y*0.6);   // 沿著牆有亮有暗，慢慢流動
  o.under=pow(saturate(1-h),2.2)*band*_UnderGlowStrength;
  o.pulse=0.75+0.35*sin(_Time.y*1.7+wp.x*0.5+wp.z*0.4);
  return o;}
 fixed4 frag(v2f i):SV_Target{fixed3 c=tex2D(_MainTex,i.uv).rgb;fixed g=tex2D(_EmissionMap,i.uv).r;
  c+=_EmissionColor.rgb*g*_Glow*i.pulse;
  c+=_UnderGlow.rgb*i.under;
  c=lerp(c,_Haze.rgb,i.haze);return fixed4(c,1);}
 ENDCG }
 }
}
