using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    internal sealed class ShadowDoubleView:MonoBehaviour
    {
        static readonly Dictionary<Material,Material> tints=new Dictionary<Material,Material>();
        static readonly Dictionary<Mesh,Mesh> whites=new Dictionary<Mesh,Mesh>();
        static readonly HashSet<Mesh> whitened=new HashSet<Mesh>();
        static int active;bool bound;static Material fallback;
        MeshFilter[] filters;Renderer[] renderers;Material[][] ghost;
        MaterialPropertyBlock block;float flashUntil,deadAt=-1;
        internal bool Dying=>deadAt>=0;
        internal void Bind()
        {
            active++;bound=true;
            filters=GetComponentsInChildren<MeshFilter>(true);renderers=new Renderer[filters.Length];ghost=new Material[filters.Length][];block=new MaterialPropertyBlock();
            for(int i=0;i<filters.Length;i++)
            {
                var r=filters[i].GetComponent<Renderer>();renderers[i]=r;if(!r)continue;
                var src=r.sharedMaterials;if(src==null||src.Length==0)src=new[]{r.sharedMaterial};ghost[i]=new Material[src.Length];
                for(int j=0;j<src.Length;j++)
                {
                    if(!src[j])
                    {
                        if(!fallback)fallback=new Material(Shader.Find("Sprites/Default")){name="Shadow empty surface",color=new Color(.28f,.12f,.47f,.46f)};
                        ghost[i][j]=fallback;continue;
                    }
                    if(!tints.TryGetValue(src[j],out var m))
                    {m=new Material(Shader.Find("Sprites/Default")){name="Shadow double spectral",color=new Color(.28f,.12f,.47f,.46f),mainTexture=src[j].mainTexture};tints[src[j]]=m;}
                    ghost[i][j]=m;
                }
            }
            var smoke=SpellFx.NewPS(transform,"Shadow edge smoke",SpellFx.BlendMat(SpellFx.TexSoft),14,true);var s=smoke.main;
            s.startColor=new Color(.16f,.035f,.27f,.34f);s.startLifetime=.5f;s.startSize=new ParticleSystem.MinMaxCurve(.10f,.21f);s.startSpeed=.12f;
            smoke.transform.localPosition=Vector3.up*.8f;SpellFx.Sphere(smoke,.34f);SpellFx.FadeOut(smoke);SpellFx.Rate(smoke,10);smoke.Play();
            LateUpdate();
        }
        void OnDestroy()
        {
            if(!bound||--active>0)return;
            foreach(var mesh in whites.Values)if(mesh)Destroy(mesh);whites.Clear();whitened.Clear();
            foreach(var material in tints.Values)if(material)Destroy(material);tints.Clear();if(fallback)Destroy(fallback);fallback=null;
        }
        internal void Flash(){flashUntil=Time.time+.13f;}
        internal void Dissolve(){if(Dying)return;deadAt=Time.time;SpellFx.ShadowDissolve(transform.position);}
        void LateUpdate()
        {
            if(filters==null)return;float fade=Dying?Mathf.Clamp01(1-(Time.time-deadAt)/.45f):1;
            var tint=Time.time<flashUntil?new Color(.78f,.48f,1f,.8f):new Color(.28f,.12f,.47f,.46f);tint.a*=fade;block.SetColor(Shader.PropertyToID("_Color"),tint);
            for(int i=0;i<filters.Length;i++)
            {
                if(!filters[i]||!renderers[i]||ghost[i]==null||ghost[i].Length==0)continue;var m=filters[i].sharedMesh;
                if(m&&!whitened.Contains(m))
                {
                    if(!whites.TryGetValue(m,out var w)){w=Object.Instantiate(m);var c=new Color[w.vertexCount];for(int j=0;j<c.Length;j++)c[j]=Color.white;w.colors=c;whites[m]=w;whitened.Add(w);}
                    filters[i].sharedMesh=w;
                }
                if(renderers[i].sharedMaterial!=ghost[i][0])renderers[i].sharedMaterials=ghost[i];
                renderers[i].SetPropertyBlock(block);if(fade<.01f)renderers[i].enabled=false;
            }
        }
    }
}
