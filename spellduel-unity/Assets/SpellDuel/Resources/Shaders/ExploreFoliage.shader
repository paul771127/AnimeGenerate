Shader "SpellDuel/ExploreFoliage"
{
 Properties { _MainTex("Generated leaf cluster",2D)="white"{} }
 SubShader
 {
  Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
  Cull Off ZWrite On
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 2.0
   #include "UnityCG.cginc"
   struct appdata{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float3 normal:TEXCOORD1;};
   sampler2D _MainTex;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
   fixed4 frag(v2f i):SV_Target{fixed4 leaf=tex2D(_MainTex,i.uv);clip(leaf.a-.4);float light=lerp(.79,1,step(.35,dot(normalize(i.normal),normalize(float3(-.4,.8,.3)))));fixed3 col=leaf.rgb*i.color.rgb*light;col=lerp(fixed3(.42,.63,.60),col,i.color.a);return fixed4(col,1);}
   ENDCG
  }
 }
}
