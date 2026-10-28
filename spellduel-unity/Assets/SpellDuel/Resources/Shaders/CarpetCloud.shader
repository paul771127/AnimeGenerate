Shader "SpellDuel/CarpetCloud" {
 Properties { _Color("Cloud light",Color)=(1,1,1,1) _Shade("Cloud shadow",Color)=(.45,.58,.72,1) _Rim("Sun silver lining",Color)=(1,.86,.6,1) _Flash("Lightning",Float)=0 _NoiseTex("Shared density",2D)="gray"{} _Detail("Density shading",Float)=.22 _RimStrength("Rim strength",Float)=.7 }
 SubShader { Tags { "RenderType"="Opaque" } Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f {float4 pos:SV_POSITION;half3 normal:TEXCOORD0;float3 world:TEXCOORD1;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 half4 _Color,_Shade,_Rim;half _Flash,_Detail,_RimStrength;sampler2D _NoiseTex;
 half4 frag(v2f i):SV_Target {half3 n=normalize(i.normal),view=normalize(_WorldSpaceCameraPos-i.world);half lit=saturate(dot(n,normalize(half3(-.4,.45,.8)))*.5+.5);half density=tex2D(_NoiseTex,i.world.xz*.022).r;lit=saturate(lit+(density-.5)*_Detail);half rim=pow(1-saturate(dot(n,view)),3)*lit;half3 c=lerp(_Shade.rgb,_Color.rgb,smoothstep(.12,.85,lit));c=lerp(c,_Rim.rgb,rim*_RimStrength);return half4(c+_Flash*half3(.35,.40,.58),1);}
 ENDCG
 }}
}
