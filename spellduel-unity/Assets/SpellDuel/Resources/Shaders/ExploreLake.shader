Shader "SpellDuel/ExploreLake"
{
 Properties { _DeepColor("Deep water",Color)=(0.035,0.23,0.26,1) _SkyColor("Reflected sky",Color)=(0.53,0.77,0.82,1) }
 SubShader
 {
  Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 2.0
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; };
   struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float2 local:TEXCOORD1; };
   fixed4 _DeepColor,_SkyColor;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.local=v.vertex.xz;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    // Periodic across 3m cells. No time input: motion belongs to the animation session.
    float2 p=i.local*2.094395;float a=sin(p.x+p.y),b=sin(p.x*2-p.y*3);
    float3 n=normalize(float3(.045*cos(p.x+p.y)+.025*cos(p.x*2-p.y*3),1,.045*cos(p.x+p.y)-.035*cos(p.x*2-p.y*3)));
    float3 view=normalize(_WorldSpaceCameraPos-i.world);float fres=pow(1-saturate(dot(n,view)),3);
    float reflectedCloud=smoothstep(.35,.85,(sin(p.x*.0+p.y)+a*.35)*.5+.5);
    fixed3 col=lerp(_DeepColor.rgb,_SkyColor.rgb,.13+fres*.67);col+=fixed3(.10,.15,.13)*(a*.04+b*.025)+reflectedCloud*fres*.055;
    float glint=pow(saturate(dot(reflect(-normalize(float3(-.4,.8,.25)),n),view)),64);
    col+=fixed3(.48,.53,.40)*glint*.5;
    return fixed4(col,.96);
   }
   ENDCG
  }
 }
}
