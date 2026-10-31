// 故事探索的特效影片（MiniMax 生成的 mp4，沒有透明）：
//   _Key = 0：黑底 → 加法混色（黑色＝透明），給光、火、光點。
//   _Key = 1：綠幕 #00FF00 → 去綠（綠色越多越透明）並壓掉綠色溢光。
// 用預乘 alpha 的混色（One, OneMinusSrcAlpha），兩種模式共用一個 shader。
Shader "SpellDuel/ExploreFxVideo" {
 Properties { _MainTex("Video",2D)="black"{} _Key("0 additive black, 1 green screen",Float)=0 _Alpha("Fade",Float)=1 }
 SubShader { Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" } ZWrite Off Cull Off Blend One OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex; half _Key, _Alpha;
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   v2f vert(appdata_base v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o; }
   half4 frag(v2f i):SV_Target {
    half3 c = tex2D(_MainTex, i.uv).rgb;
    // 邊緣淡出：影片四周的雜訊不要畫出硬邊
    half2 e = min(i.uv, 1 - i.uv); half edge = saturate(min(e.x, e.y) * 12);
    if (_Key < 0.5) return half4(c * _Alpha * edge, 0);
    half other = max(c.r, c.b); half g = c.g - other;
    half a = (1 - smoothstep(0.12, 0.32, g)) * _Alpha * edge;
    c.g = min(c.g, other + 0.05);   // 去綠色溢光
    return half4(c * a, a);
   }
   ENDCG
  }
 }
}
