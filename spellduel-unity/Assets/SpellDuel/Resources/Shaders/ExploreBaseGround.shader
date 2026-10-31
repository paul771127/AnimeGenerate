Shader "SpellDuel/ExploreBaseGround"
{
 // 故事探索的底層地面（使用者回報樹、石頭底下透出真實地板，之後又回報一格一格的方塊很突兀）：
 // 整張地圖一片連續、完全不透明的地面，貼圖用網格的 UV 無縫重複，沒有格子邊。畫在路、地貼、湖面之前。
 Properties { _MainTex("Ground",2D)="white" {} _Tint("Tint",Color)=(1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Transparent-45" "RenderType"="Opaque" "IgnoreProjector"="True" }
  Cull Off ZWrite Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 2.0
   #include "UnityCG.cginc"
   sampler2D _MainTex; fixed4 _Tint;
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
   fixed4 frag(v2f i):SV_Target
   {
    // 兩個不同尺度的取樣混在一起：遠看不會有明顯的重複花紋
    fixed3 a=tex2D(_MainTex,i.uv).rgb, b=tex2D(_MainTex,i.uv*0.23+0.37).rgb;
    return fixed4(lerp(a,b,0.35)*_Tint.rgb,1);
   }
   ENDCG
  }
 }
}
