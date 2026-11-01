Shader "SpellDuel/DungeonExploreFloor"
{
 Properties { _Theme("Theme",Float)=0 _Room("Room",Float)=0 _Accent("Accent",Color)=(1,.4,.2,1)  _MainTex("Ground detail",2D)="white"{} _PathColor("Lane",Color)=(.76,.59,.34,1) _VergeColor("Blocked verge",Color)=(.2,.32,.12,1) }
 SubShader {
 Tags { "Queue"="Transparent-35" "RenderType"="Transparent" }
 Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _PathColor,_VergeColor,_TexOffset,_Props[16];int _PropCount;float _Theme,_Room,_RoomEdge;float4 _Accent;
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 links:TEXCOORD1;};
 struct v2f {float4 pos:SV_POSITION;float2 p:TEXCOORD0;float4 links:TEXCOORD1;};
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.p=v.uv;o.links=v.links;return o;}
 float bit(float mask,float place){return fmod(floor(floor(mask+.5)/place),2);}
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
 float2 p=i.p+_TexOffset.xy;float grain=tex2D(_MainTex,p/3).r;
 float3 base=_Theme<.5?float3(.29,.30,.35):_Theme<1.5?float3(.39,.40,.23):_Theme<2.5?float3(.65,.66,.47):_Theme<3.5?float3(.64,.49,.30):_Theme<4.5?float3(.66,.43,.26):float3(.34,.40,.43);
 float detail=.87+grain*.25;
 if(_Theme>4.5){float2 q=abs(frac(p*2)-.5);float grate=step(.13,q.x)*step(.13,q.y);detail*=lerp(.35,1,grate);float seam=min(frac(p.x/2),frac(p.y/2));detail*=lerp(.55,1,smoothstep(.01,.035,seam));}
 else if(_Room>.5){float row=floor(p.y/1.2);float2 q=frac(float2(p.x/1.6+row*.5,p.y/1.2));float joint=min(min(q.x,1-q.x),min(q.y,1-q.y));detail*=lerp(.69,1,smoothstep(.015,.045,joint));}
 else if(_Theme>1.5&&_Theme<2.5){float ripple=pow(saturate(sin(length(p-float2(3,4))*6-_Time.y*1.2)),18);base=lerp(base,float3(.30,.62,.67),.25+.1*sin(p.x*.6));detail+=ripple*.07;}
 float rim=100;if(bit(_RoomEdge,1)>.5)rim=min(rim,h-i.p.x);if(bit(_RoomEdge,2)>.5)rim=min(rim,h+i.p.x);if(bit(_RoomEdge,4)>.5)rim=min(rim,h-i.p.y);if(bit(_RoomEdge,8)>.5)rim=min(rim,h+i.p.y);
 float border=(1-smoothstep(.13,.19,rim))*step(.055,rim)*_Room;
 float3 road=base*detail*lerp(.78,1,edge);road=lerp(road,_Accent.rgb*.7,border*.8);
 float3 verge=base*.48;return fixed4(lerp(verge,road,lane),.98);

 }
 ENDCG }
 }
}
