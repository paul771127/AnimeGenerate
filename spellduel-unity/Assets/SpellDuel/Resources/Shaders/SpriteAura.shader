Shader "SpellDuel/SpriteAura"
{
 // 2D 敵人身上冒出的光（魔劍士惡靈攻擊加速時的紅光）：取圖的外形往外擴、往上飄，疊加發光
 Properties { _MainTex("Sprite",2D)="white"{} _Color("Color",Color)=(1,0.1,0.05,1) _Spread("Spread (px)",Float)=10 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color; float _Spread;
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   v2f vert(appdata_base v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; return o; }
   fixed4 frag(v2f i):SV_Target{
    float2 px=_MainTex_TexelSize.xy*_Spread;
    float t=_Time.y*3.0;
    float a=0;
    [unroll] for(int k=0;k<12;k++){
     float ang=k*0.5236;
     float2 d=float2(cos(ang),sin(ang));
     // 往上飄：下方的取樣點拉遠（光從身體往上冒）、加一點閃動
     float r=1.0+0.35*sin(t+k*1.7);
     float2 o=d*px*r; o.y-=px.y*(0.6+0.4*sin(t*1.3+k));
     a=max(a,tex2D(_MainTex,i.uv-o).a*0.8);
     a=max(a,tex2D(_MainTex,i.uv-o*0.5).a);
    }
    float inner=tex2D(_MainTex,i.uv).a;
    float g=a*(1.0-0.55*inner);
    return fixed4(_Color.rgb, g*_Color.a);
   }
   ENDCG
  }
 }
}
