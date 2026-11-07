Shader "SceneKit/LavaFlow005"
{
 Properties { _MainTex("Main atlas",2D)="white"{} _EmissionMap("Linear emission",2D)="black"{} _FlowSpeed("Downstream V/second",Float)=0.18 _PreviewTime("Preview override (-1 = game time)",Float)=-1 _Glow("Glow",Float)=0.6 }
 SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" }
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 sampler2D _MainTex,_EmissionMap;float _FlowSpeed,_PreviewTime,_Glow;
 struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
 fixed4 frag(v2f i):SV_Target { float t=_PreviewTime<0?_Time.y:_PreviewTime;
  float2 uv=float2(i.uv.x,frac(i.uv.y-t*_FlowSpeed));
  fixed3 c=tex2D(_MainTex,uv).rgb;fixed e=tex2D(_EmissionMap,uv).r;
  return fixed4(c+fixed3(0.26,0.10,0.025)*e*_Glow,1);
 }
 ENDCG }
 }
}
