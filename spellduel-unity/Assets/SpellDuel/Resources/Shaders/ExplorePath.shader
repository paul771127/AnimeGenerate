Shader "SpellDuel/ExplorePath"
{
 Properties { _MainTex("Ground detail",2D)="white"{} _PathColor("Lane",Color)=(.76,.59,.34,1) _VergeColor("Blocked verge",Color)=(.2,.32,.12,1) }
 SubShader {
 Tags { "Queue"="Transparent-35" "RenderType"="Transparent" }
 Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _PathColor,_VergeColor,_TexOffset,_Props[16];int _PropCount;
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 links:TEXCOORD1;};
 struct v2f {float4 pos:SV_POSITION;float2 p:TEXCOORD0;float4 links:TEXCOORD1;};
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.p=v.uv;o.links=v.links;return o;}
 float bit(float mask,float place){return fmod(floor(mask/place),2);}
 fixed4 frag(v2f i):SV_Target {
 float h=i.links.z,f=i.links.w,d=100;float4 open=float4(bit(i.links.x,1),bit(i.links.x,2),bit(i.links.x,4),bit(i.links.x,8));
 if(open.x<.5)d=min(d,h-.35-i.p.x);if(open.y<.5)d=min(d,h-.35+i.p.x);
 if(open.z<.5)d=min(d,h-.35-i.p.y);if(open.w<.5)d=min(d,h-.35+i.p.y);
 for(int k=0;k<4;k++) {float sx=k%2==0?1:-1,sz=k<2?1:-1;float2 a=i.p*float2(sx,sz);float ox=sx>0?open.x:open.y,oz=sz>0?open.z:open.w;
 if(ox<.5&&oz<.5&&a.x>h-f&&a.y>h-f)d=min(d,f-.35-length(a-(h-f)));
 if(ox>.5&&oz>.5&&bit(i.links.y,pow(2,k))<.5)d=min(d,length(h-a)-.35);
 }
 for(int n=0;n<16;n++){if(n>=_PropCount)break;d=min(d,length(i.p-_Props[n].xy)-_Props[n].z);}
 float aa=max(fwidth(d),.008),lane=smoothstep(-aa,aa,d);float edge=smoothstep(0,.28,d);
 fixed3 tex=tex2D(_MainTex,(i.p+_TexOffset.xy)/3).rgb;float grain=dot(tex,float3(.3,.59,.11));
 fixed3 road=_PathColor.rgb*(.85+grain*.32)*lerp(.73,1,edge);
 fixed3 verge=_VergeColor.rgb*(.85+grain*.3);return fixed4(lerp(verge,road,lane),lerp(.85,.97,lane));
 }
 ENDCG }
 }
}
