Shader "SpellDuel/CharOutline"
{
 // 角色生成器的反轉外殼描邊（OUTLINE_* 網格）：不打光的單色，只畫正面（外殼的法線已經翻過來）
 Properties { _Color("Color",Color)=(0.012,0.008,0.02,1) }
 SubShader {
 Tags { "Queue"="Geometry" "RenderType"="Opaque" }
 Cull Back
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 fixed4 _Color;
 float4 vert(float4 v:POSITION):SV_POSITION{return UnityObjectToClipPos(v);}
 fixed4 frag():SV_Target{return _Color;}
 ENDCG }
 }
}
