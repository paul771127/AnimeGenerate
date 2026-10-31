Shader "SpellDuel/ExploreGround"
{
 Properties { _MainTex("Generated ground",2D)="white" {} _Opacity("AR floor opacity",Range(0,1))=.42 }
 SubShader
 {
  Tags { "Queue"="Transparent-40" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 2.0
   #include "UnityCG.cginc"
   sampler2D _MainTex;half _Opacity;
   struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
   struct v2f { float4 pos:SV_POSITION;float2 local:TEXCOORD0; };
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.local=v.uv;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    fixed3 c=tex2D(_MainTex,i.local/3+.5).rgb;
    float2 edge=saturate((1.53-abs(i.local))/.06);
    edge=edge*edge*(3-2*edge);
    // Complementary optical coverage: overlapping skirts do not create dark grid seams.
    half alpha=1-pow(1-_Opacity,edge.x*edge.y);
    return fixed4(c,alpha);
   }
   ENDCG
  }
 }
}
