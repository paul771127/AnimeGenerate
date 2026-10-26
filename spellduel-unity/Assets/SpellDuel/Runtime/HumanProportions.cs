using UnityEngine;
namespace SpellDuel
{
    internal static class HumanProportions
    {
        // Player haircut edits must not refit the accepted face or move its rig.
        static Mesh FittingHair(string id,Portraits.Persona p)=>PlayerClassArt.IsPlayer(id)?AnimeHair.MeshFor("player_identity",p):HeadwearArt.HairFor(id,p);
        // Hair above the skull counts; hanging ponytails below the chin do not add head height.
        internal static void Extents(string id,Portraits.Persona p,out float chin,out float top,out float width)
        {
            chin=1;top=-1;float lo=1,hi=-1;
            var skull=AnimeFace.FittingVertices(p);for(int i=0;i<skull.Length;i++){chin=Mathf.Min(chin,skull[i].y);top=Mathf.Max(top,skull[i].y);lo=Mathf.Min(lo,skull[i].x);hi=Mathf.Max(hi,skull[i].x);}
            var hair=FittingHair(id,p).vertices;for(int i=0;i<hair.Length;i++)top=Mathf.Max(top,hair[i].y);width=hi-lo;
        }
        internal static void Fit(Transform head,string id,float height,float actorScale=1)
        {
            var p=Portraits.PersonaFor(id);Extents(id,p,out var chin,out var top,out var width);
            // A single adult reference, with the shorter adult kept within the 6.5-head limit.
            float target=id=="小葉"?height/5f:height<1.7f?.246f:.258f;
            float sy=target/(top-chin)/actorScale,sx=(id=="小葉"?.196f:.202f)/width/actorScale;
            // Preserve the original head/hair/hat upper envelope at the same total actor height.
            float oldTop=-10;var filters=head.GetComponentsInChildren<MeshFilter>();
            for(int i=0;i<filters.Length;i++)
            {
                if(head.Find("Halo")!=null&&filters[i].transform.IsChildOf(head.Find("Halo")))continue;
                var vs=PlayerClassArt.IsPlayer(id)&&filters[i].gameObject.name=="整體髮型"?FittingHair(id,p).vertices:filters[i].sharedMesh.vertices;var m=head.parent.worldToLocalMatrix*filters[i].transform.localToWorldMatrix;
                for(int j=0;j<vs.Length;j++)oldTop=Mathf.Max(oldTop,m.MultiplyPoint3x4(vs[j]).y);
            }
            head.localScale=new Vector3(sx,sy,sy);
            float newTop=-10;for(int i=0;i<filters.Length;i++)
            {
                if(head.Find("Halo")!=null&&filters[i].transform.IsChildOf(head.Find("Halo")))continue;
                var vs=PlayerClassArt.IsPlayer(id)&&filters[i].gameObject.name=="整體髮型"?FittingHair(id,p).vertices:filters[i].sharedMesh.vertices;var m=head.parent.worldToLocalMatrix*filters[i].transform.localToWorldMatrix;
                for(int j=0;j<vs.Length;j++)newTop=Mathf.Max(newTop,m.MultiplyPoint3x4(vs[j]).y);
            }
            head.localPosition+=Vector3.up*(oldTop-newTop);
        }
        internal static void NormalizeProjected(Transform head,string id,Transform actor)
        {
            var p=Portraits.PersonaFor(id);float top,chin;Projected(head,id,p,actor,out top,out chin);
            float target=id=="小葉"?top/5f:Mathf.Clamp(top/6.75f,.245f,.260f);
            float before=Envelope(head,actor,id,p);head.localScale*=target/(top-chin);
            // Match projected adult skull width as well as height; retain jaw/cheek geometry.
            var skull=AnimeFace.FittingVertices(p);var mx=actor.worldToLocalMatrix*head.localToWorldMatrix;float lo=10,hi=-10;
            for(int i=0;i<skull.Length;i++){float x=mx.MultiplyPoint3x4(skull[i]).x;lo=Mathf.Min(lo,x);hi=Mathf.Max(hi,x);}
            var scale=head.localScale;scale.x*=(id=="小葉"?.190f:.198f)/(hi-lo);head.localScale=scale;
            float after=Envelope(head,actor,id,p);
            head.position+=actor.TransformPoint(new Vector3(0,before-after,0))-actor.TransformPoint(Vector3.zero);
        }
        static void Projected(Transform head,string id,Portraits.Persona p,Transform actor,out float top,out float chin)
        {
            top=-10;chin=10;var m=actor.worldToLocalMatrix*head.localToWorldMatrix;var skull=AnimeFace.FittingVertices(p);
            float localChin=10;for(int i=0;i<skull.Length;i++){var v=m.MultiplyPoint3x4(skull[i]);chin=Mathf.Min(chin,v.y);top=Mathf.Max(top,v.y);localChin=Mathf.Min(localChin,skull[i].y);}
            var hair=FittingHair(id,p).vertices;for(int i=0;i<hair.Length;i++)if(hair[i].y>=localChin)top=Mathf.Max(top,m.MultiplyPoint3x4(hair[i]).y);
        }
        static float Envelope(Transform head,Transform actor,string id,Portraits.Persona p)
        {
            float top=-10;var halo=head.Find("Halo");var filters=head.GetComponentsInChildren<MeshFilter>();
            for(int i=0;i<filters.Length;i++)
            {
                if(halo!=null&&filters[i].transform.IsChildOf(halo))continue;
                var vs=PlayerClassArt.IsPlayer(id)&&filters[i].gameObject.name=="整體髮型"?FittingHair(id,p).vertices:filters[i].sharedMesh.vertices;var m=actor.worldToLocalMatrix*filters[i].transform.localToWorldMatrix;
                for(int j=0;j<vs.Length;j++)top=Mathf.Max(top,m.MultiplyPoint3x4(vs[j]).y);
            }
            return top;
        }
    }
}
