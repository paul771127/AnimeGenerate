Shader "SpellDuel/CastleIllustration"
{
 Properties { _MainTex("Atlas",2D)="white"{} _AtlasRect("Atlas rect",Vector)=(0,0,1,1) _Tiling("Repeat",Vector)=(1,1,0,0) _Background("Backdrop",Float)=0 _ZWrite("Depth write",Float)=1 }
 SubShader {
 Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
 Cull Off ZWrite [_ZWrite] Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _AtlasRect,_Tiling;float _Background;
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
 struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float fog:TEXCOORD1;};
 // 距離漸暗（使用者要求：遠到近要由黑慢慢變出顏色，不能一塊黑一塊有色）：9m 開始暗、21m 全黑（場景只載入約 24m 內，載入邊界已經是黑的）
 // 數字和 ExploreArt.CastleFogNear/Far、SpriteEnemyView、DungeonExploreFloor 一致
 v2f vert(appdata v){v2f o;o.uv=v.uv;o.pos=UnityObjectToClipPos(v.vertex);
 float3 wp=mul(unity_ObjectToWorld,v.vertex).xyz;o.fog=1-smoothstep(9,21,length(wp-_WorldSpaceCameraPos));
 if(_Background>.5){float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;float3 d=world-_WorldSpaceCameraPos;world=_WorldSpaceCameraPos+d*min(1,(_ProjectionParams.z*.9)/max(length(d),.001));o.pos=UnityWorldToClipPos(world);
 #if defined(UNITY_REVERSED_Z)
 o.pos.z=o.pos.w*.0001;
 #else
 o.pos.z=o.pos.w*.9999;
 #endif
 }return o;}
 fixed4 frag(v2f i):SV_Target{float2 uv=frac(min(i.uv,.99999)*_Tiling.xy);fixed4 c=tex2D(_MainTex,_AtlasRect.xy+uv*_AtlasRect.zw);clip(c.a-.08);c.rgb*=lerp(i.fog,.05,_Background);return c;}
 ENDCG }
 }
}
