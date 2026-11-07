Shader "SpellDuel/StageKitGround"
{
 // 生成器的手繪地面（Resources/SceneKit/*/cel_ground_*）：副本大地圖每一格的地面，照世界座標每 _Tile 公尺重複一次（格子之間沒有接縫），遠處淡成霧色。
 Properties { _MainTex("Ground",2D)="white"{} _Tile("Repeat (m)",Float)=3 _Haze("Haze",Color)=(0.4,0.42,0.45,1) _HazeNear("Haze near",Float)=14 _HazeFar("Haze far",Float)=40 _HazeMax("Haze max",Float)=0.55 }
 SubShader {
 Tags { "Queue"="Geometry" "RenderType"="Opaque" }
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _TexOffset,_Haze;float _Tile,_HazeNear,_HazeFar,_HazeMax;
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
 struct v2f{float4 pos:SV_POSITION;float2 p:TEXCOORD0;float haze:TEXCOORD1;};
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.p=v.uv;float3 wp=mul(unity_ObjectToWorld,v.vertex).xyz;o.haze=smoothstep(_HazeNear,_HazeFar,length(wp-_WorldSpaceCameraPos))*_HazeMax;return o;}
 fixed4 frag(v2f i):SV_Target{fixed3 c=tex2D(_MainTex,(i.p+_TexOffset.xy)/_Tile).rgb;return fixed4(lerp(c,_Haze.rgb,i.haze),1);}
 ENDCG }
 }
}
