Shader "SpellDuel/ReadableSpellSolid"
{
 Properties { _Color("Color",Color)=(1,1,1,1) _MainTex("Texture",2D)="white" {} _PolishFade("Near-view coverage",Range(0,1))=1 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Tags { "LightMode"="ForwardBase" }
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   sampler2D _MainTex;float4 _MainTex_ST;fixed4 _Color;float _PolishFade;
   struct input {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct output {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;float4 screen:TEXCOORD2;fixed4 color:COLOR;};
   output vert(input v){output o;o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.screen=ComputeScreenPos(o.vertex);o.color=v.color;return o;}
   fixed4 frag(output i):SV_Target
   {
    float2 pixel=floor(i.screen.xy/max(.0001,i.screen.w)*_ScreenParams.xy);
    // Stable 4x4 ordered coverage: depth remains correct and no alpha-sort reversal.
    float2 a=fmod(pixel,2),b=fmod(floor(pixel/2),2);
    float lo=a.x*2+a.y*3-4*a.x*a.y,hi=b.x*2+b.y*3-4*b.x*b.y;
    clip(_PolishFade-(lo*4+hi+.5)/16);
    float light=dot(normalize(i.normal),normalize(_WorldSpaceLightPos0.xyz));
    fixed3 baseColor=tex2D(_MainTex,i.uv).rgb*_Color.rgb*i.color.rgb;
    return fixed4(baseColor*lerp(.56,1,step(.12,light)),1);
   }
   ENDCG
  }
 }
}
