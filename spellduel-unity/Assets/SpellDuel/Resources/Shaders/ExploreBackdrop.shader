Shader "SpellDuel/ExploreBackdrop"
{
 SubShader
 {
  Tags {"Queue"="Background" "RenderType"="Background"}
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 2.0
   #include "UnityCG.cginc"
   struct appdata{float4 vertex:POSITION;fixed4 color:COLOR;};
   struct v2f{float4 pos:SV_POSITION;fixed4 color:COLOR;};
   v2f vert(appdata v)
   {
    v2f o;float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;float3 delta=world-_WorldSpaceCameraPos;
    // Preserve the direction of the world-space scenery while keeping it inside the AR far plane.
    world=_WorldSpaceCameraPos+delta*min(1,(_ProjectionParams.z*.9)/max(length(delta),.001));o.pos=UnityWorldToClipPos(world);
    #if defined(UNITY_REVERSED_Z)
     o.pos.z=o.pos.w*.0001;
    #else
     o.pos.z=o.pos.w*.9999;
    #endif
    o.color=v.color;return o;
   }
   fixed4 frag(v2f i):SV_Target{return i.color;}
   ENDCG
  }
 }
}
