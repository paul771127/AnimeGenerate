using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public partial class CharacterRig
    {
        static readonly Dictionary<string,Mesh> gearArtMeshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,Material> gearArtTints=new Dictionary<string,Material>();

        partial void BuildGearArt(GearLook g)
        {
            // An inactive owner may defer OnDestroy until after the replacement has been built.
            foreach(var pending in body.GetComponentsInChildren<GearVisualLease>(true))if(!pending.gameObject.activeSelf)pending.Release();
            var previousRenderers=rends;var previousMaterials=origMats;
            var weaponOriginals=new Dictionary<Renderer,Material>();var equipmentOverrides=new Dictionary<Renderer,Material>();
            var lease=GearPart(new GameObject("Equipment material lease"));lease.transform.SetParent(body,false);
            var lifetime=lease.AddComponent<GearVisualLease>();
            lifetime.Restore=()=>
            {
                if(this==null)return;
                rends=previousRenderers;origMats=previousMaterials;
                if(!flashing)foreach(var pair in weaponOriginals)if(pair.Key)pair.Key.sharedMaterial=pair.Value;
            };
            string id=advancedId??classId;
            if(g[Slot.Head].Has)AddGear(head,id,"head",g[Slot.Head],null);
            if(g[Slot.Body].Has||g.FullSet!=null)AddGear(spine,id,"body",g[Slot.Body],g.FullSet);
            if(g[Slot.Feet].Has){AddGear(knL,id,"feet",g[Slot.Feet],null);AddGear(knR,id,"feet",g[Slot.Feet],null);}
            if(g[Slot.Trinket].Has)AddGear(elL,id,"trinket",g[Slot.Trinket],null);
            if(g[Slot.Weapon].Has)
            {
                GearLook.PieceColors(g[Slot.Weapon],out var main,out var accent);
                Transform decorated=null;
                foreach(var hand in new[]{handR,handL})for(int i=0;i<hand.childCount;i++)
                {
                    var weapon=hand.GetChild(i);string n=weapon.gameObject.name;
                    if(n!="Sword"&&n!="Bow"&&n!="Staff"&&n!="Dagger"&&n!="CrescentBlade"&&n!="HolyLance"&&n!="WarAxe")continue;
                    if(decorated==null)decorated=weapon;
                    foreach(var renderer in weapon.GetComponentsInChildren<Renderer>())
                    {
                        var source=renderer.sharedMaterial;
                        int previous=Array.IndexOf(previousRenderers,renderer);if(flashing&&previous>=0)source=previousMaterials[previous];
                        // Keep luminous orbs, strings and translucent effects on their existing materials.
                        if(source==null||source.shader!=litShader)continue;
                        string key=source.GetInstanceID()+"/"+ColorUtility.ToHtmlStringRGBA(main);
                        if(!gearArtTints.TryGetValue(key,out var tint)){tint=new Material(source){name="Equipped weapon "+key,color=main};gearArtTints[key]=tint;}
                        weaponOriginals[renderer]=source;equipmentOverrides[renderer]=tint;renderer.sharedMaterial=tint;
                    }
                }
                if(decorated!=null)AddGear(decorated,id,"weapon",g[Slot.Weapon],null);
            }
            // Include equipment in the existing hit-flash and visibility paths, without changing the animator.
            // Destroy is deferred in play mode: do not re-enrol inactive equipment from the previous look.
            var current=new List<Renderer>();
            foreach(var renderer in previousRenderers)if(renderer)current.Add(renderer);
            foreach(var go in gearParts)foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))if(!current.Contains(renderer))current.Add(renderer);
            rends=current.ToArray();origMats=new Material[rends.Length];
            for(int i=0;i<rends.Length;i++)
            {
                var renderer=rends[i];int previous=Array.IndexOf(previousRenderers,renderer);
                origMats[i]=flashing&&previous>=0&&!weaponOriginals.ContainsKey(renderer)?previousMaterials[previous]:renderer.sharedMaterial;
                if(previous<0)equipmentOverrides[renderer]=origMats[i];
            }
            foreach(var go in gearParts)foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.enabled=visible;
            if(flashing){flashing=false;SetFlash(true);}
            var equippedRenderers=rends;var equippedMaterials=origMats;
            lifetime.Apply=()=>
            {
                if(this==null)return;rends=equippedRenderers;origMats=equippedMaterials;
                // Eyes and expressions may have changed material since equipping. Restore only owned overrides.
                foreach(var pair in equipmentOverrides)if(pair.Key)pair.Key.sharedMaterial=pair.Value;
                foreach(var go in gearParts)if(go)foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))renderer.enabled=visible;
                if(flashing){flashing=false;SetFlash(true);}
            };
            if(!lease.activeInHierarchy)lifetime.Suspend();
        }
        void AddGear(Transform parent,string id,string slot,GearLook.Piece piece,string full)
        {
            string key=id+"/"+slot+"/"+piece.rarity+"/"+piece.set+"/"+full;
            if(!gearArtMeshes.TryGetValue(key,out var mesh)){mesh=MakeGearMesh(id,slot,piece,full);gearArtMeshes[key]=mesh;}
            GearPart(AnimeFace.Part(parent,"Equipment "+slot,mesh,Vector3.zero,AnimeFace.Material(Color.white,4)));
        }
        sealed class GearSurfaceData { internal Vector3[] v;internal int[] t; }
        static readonly Dictionary<Mesh,GearSurfaceData> gearSurfaces=new Dictionary<Mesh,GearSurfaceData>();
        static float GearSurface(Mesh mesh,float x,float y)
        {
            if(!gearSurfaces.TryGetValue(mesh,out var data)){data=new GearSurfaceData{v=mesh.vertices,t=mesh.triangles};gearSurfaces[mesh]=data;}
            var v=data.v;var t=data.t;float z=-10;
            for(int i=0;i<t.Length;i+=3)
            {
                var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];float den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(den)<1e-9f)continue;
                float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/den,w=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/den;
                if(u>=-.0001f&&w>=-.0001f&&u+w<=1.0001f)z=Mathf.Max(z,u*a.z+w*b.z+(1-u-w)*c.z);
            }
            return z>-1?z:.13f;
        }
        static Vector3 GearPanelPoint(Mesh surface,float top,float bottom,float width,float t,float u)
        {
            float taper=t<.25f?Mathf.Lerp(.78f,1,t*4):Mathf.Lerp(1,.68f,(t-.25f)/.75f);
            float x=u*width*taper,y=Mathf.Lerp(top,bottom,t)+(t==0?-.018f:t==1?.018f:0)*u*u;
            return new Vector3(x,y,GearSurface(surface,x,y)+.009f);
        }
        static void GearPanel(OutfitArt.Builder b,Mesh surface,float top,float bottom,float width,Color main,Color edge)
        {
            const int rows=4,cols=8;int first=b.V.Count,triangleStart=b.T.Count;
            for(int j=0;j<=rows;j++)for(int i=0;i<=cols;i++)
            {
                b.V.Add(GearPanelPoint(surface,top,bottom,width,j/(float)rows,i/(float)cols*2-1));b.C.Add(main);
                if(i<cols&&j<rows){int k=first+j*(cols+1)+i;b.T.AddRange(new[]{k,k+cols+1,k+1,k+1,k+cols+1,k+cols+2});}
            }
            // A folded lapel may sit between grid vertices. Fit triangle interiors as well as corners.
            for(int pass=0;pass<3;pass++)for(int k=triangleStart;k<b.T.Count;k+=3)
            {
                int ia=b.T[k],ib=b.T[k+1],ic=b.T[k+2];var a=b.V[ia];var c=b.V[ib];var d=b.V[ic];float lift=0;
                foreach(var p in new[]{(a+c+d)/3,(a+c)*.5f,(a+d)*.5f,(c+d)*.5f})lift=Mathf.Max(lift,GearSurface(surface,p.x,p.y)+.006f-p.z);
                if(lift>0){var up=new Vector3(0,0,lift+.001f);b.V[ia]+=up;b.V[ib]+=up;b.V[ic]+=up;}
            }
            for(int side=-1;side<=1;side+=2)for(int j=0;j<rows;j++)
            {
                int edgeIndex=side<0?0:cols;var a=b.V[first+j*(cols+1)+edgeIndex]+new Vector3(0,0,.003f);
                var c=b.V[first+(j+1)*(cols+1)+edgeIndex]+new Vector3(0,0,.003f);b.Seam(a,c,.003f,edge);
            }
        }
        static void GearGem(OutfitArt.Builder b,Vector3 at,float size,Color color)
        {
            var ring=new[]{new Vector3(-size,0,0),new Vector3(0,size*1.4f,0),new Vector3(size,0,0),new Vector3(0,-size*1.4f,0)};
            for(int i=0;i<4;i++){int n=b.V.Count;b.V.Add(at+ring[i]);b.V.Add(at+ring[(i+1)%4]);b.V.Add(at+new Vector3(0,0,size*.7f));var c=color*(i%2==0?1:.70f);c.a=1;b.C.Add(c);b.C.Add(c);b.C.Add(c);b.T.AddRange(new[]{n,n+2,n+1});}
        }
        static void GearMotif(OutfitArt.Builder b,string set,Vector3 at,float s,Color c)
        {
            if(string.IsNullOrEmpty(set))return;
            if(set=="flame")
            {
                for(int side=-1;side<=1;side+=2)b.Tube(new[]{at+new Vector3(side*s*.3f,0,0),at+new Vector3(side*s*.65f,s*.6f,0),at+new Vector3(side*s*.45f,s,0)},new[]{s*.15f,s*.11f,.001f},new[]{s*.12f,s*.08f,.001f},c,6);
            }
            else if(set=="tide")
            {
                for(int k=-2;k<=2;k++)b.Seam(at+new Vector3(k*s*.12f,-s*.35f,0),at+new Vector3(k*s*.3f,s*.35f,s*.12f),s*.055f,c);
            }
            else if(set=="gladiator")
            {
                b.Badge(at.x,at.y,at.z,s,s*.85f,c);for(int k=-1;k<=1;k++)b.Badge(at.x+k*s*.22f,at.y+s*.7f,at.z,s*.12f,s*.45f,c);
            }
            else if(set=="loess")
            {
                for(int k=-1;k<=1;k++)b.Seam(at+new Vector3(-s*.45f,k*s*.23f,0),at+new Vector3(s*.45f,k*s*.23f+s*.1f,0),s*.07f,c);
            }
            else if(set=="guard")
                b.Panel(at+new Vector3(-s*.48f,s*.4f,0),at+new Vector3(s*.48f,s*.4f,0),at+new Vector3(-s*.12f,-s*.5f,s*.1f),at+new Vector3(s*.12f,-s*.5f,s*.1f),c,.003f,2,2);
            else
            {
                int count=set=="gale"?3:2;for(int k=0;k<count;k++){float x=(k-(count-1)*.5f)*s*.32f;b.Panel(at+new Vector3(x+s*.08f,s*.6f,0),at+new Vector3(x+s*.22f,0,s*.06f),at+new Vector3(x-s*.16f,s*.05f,0),at+new Vector3(x,-s*.4f,0),c,.002f,2,2);}
            }
        }
        static Mesh MakeGearMesh(string id,string slot,GearLook.Piece p,string full)
        {
            var b=new OutfitArt.Builder();GearLook.PieceColors(p,out var main,out var accent);int r=Mathf.Clamp(p.rarity,0,4);
            if(slot=="head")
            {
                var persona=Portraits.PersonaFor(id);float y=HeadwearArt.Has(id)?HeadwearArt.Seat(id,persona)+.022f:.095f;
                float radius=HeadwearArt.FitRadius(id,persona,y)+(HeadwearArt.Has(id)?.004f:.012f),front=radius*(HeadwearArt.Has(id)?.95f:1);
                b.Loft(new[]{y+.012f,y-.012f},new[]{radius,radius},new[]{front,front},main,0,0,12);
                b.Badge(0,y,front+.003f,.035f+r*.008f,.028f+r*.009f,accent);
                if(r>=3)GearGem(b,new Vector3(0,y+.002f,front+.012f),.013f,accent);
                if(r==4)for(int side=-1;side<=1;side+=2)b.Seam(new Vector3(side*.045f,y,radius),new Vector3(side*.07f,y+.035f,radius-.01f),.007f,accent);
                GearMotif(b,p.set,new Vector3(0,y+.008f,front+.018f),.035f,accent);
            }
            else if(slot=="body")
            {
                var torso=OutfitArt.MeshFor(id,"torso",Skin);
                if(p.Has){GearPanel(b,torso,.405f,.205f,.125f,main,accent);float z=GearSurface(torso,0,.30f)+.019f;b.Badge(0,.30f,z,.032f+r*.007f,.04f+r*.009f,accent);if(r>=2)b.Seam(new Vector3(0,.37f,GearSurface(torso,0,.37f)+.014f),new Vector3(0,.23f,GearSurface(torso,0,.23f)+.014f),.003f,accent);if(r>=3)GearGem(b,new Vector3(0,.30f,z+.008f),.017f,accent);GearMotif(b,p.set,new Vector3(0,.34f,z+.012f),.045f,accent);}
                if(full!=null)
                {
                    GearLook.SetColors(full,out var setMain,out var setAccent);GearPanel(b,torso,.15f,.11f,.12f,setMain,setAccent);
                    b.Panel(new Vector3(-.12f,.15f,-.14f),new Vector3(.12f,.15f,-.14f),new Vector3(-.12f,.11f,-.14f),new Vector3(.12f,.11f,-.14f),setAccent,.003f,2,1);
                    GearMotif(b,full,new Vector3(0,.13f,GearSurface(torso,0,.13f)+.019f),.033f,setAccent);
                }
            }
            else if(slot=="feet")
            {
                var calf=OutfitArt.MeshFor(id,"calfL",Skin);GearPanel(b,calf,-.065f,-.265f,.039f,main,accent);
                float z=GearSurface(calf,0,-.12f)+.020f;b.Badge(0,-.12f,z,.035f,.018f,accent);
                if(r>=2)b.Seam(new Vector3(0,-.08f,z),new Vector3(0,-.24f,GearSurface(calf,0,-.24f)+.015f),.004f,accent);
                if(r>=3)GearGem(b,new Vector3(0,-.13f,z+.007f),.012f,accent);GearMotif(b,p.set,new Vector3(0,-.20f,GearSurface(calf,0,-.20f)+.020f),.025f,accent);
            }
            else if(slot=="trinket")
            {
                float rx=.055f,rz=.058f;foreach(var v in OutfitArt.MeshFor(id,"foreL",Skin).vertices)if(Mathf.Abs(v.y+.215f)<.025f){rx=Mathf.Max(rx,Mathf.Abs(v.x)+.008f);rz=Mathf.Max(rz,Mathf.Abs(v.z-.006f)+.008f);}
                b.Loft(new[]{-.205f,-.226f},new[]{rx,rx},new[]{rz,rz},main,0,.006f,10);b.Badge(0,-.215f,rz+.015f,.027f+r*.003f,.027f+r*.004f,accent);
                if(r>=3)GearGem(b,new Vector3(0,-.215f,rz+.024f),.011f,accent);GearMotif(b,p.set,new Vector3(0,-.215f,rz+.029f),.025f,accent);
            }
            else
            {
                for(int k=0;k<3;k++)b.Loft(new[]{-.04f+k*.025f,-.028f+k*.025f},new[]{.028f,.028f},new[]{.028f,.028f},k%2==0?main:accent,0,0,8);
                if(r>=2)b.Badge(0,.065f,.032f,.055f,.024f,accent);if(r>=3)GearGem(b,new Vector3(0,.065f,.041f),.014f,accent);if(r==4)for(int side=-1;side<=1;side+=2)b.Seam(new Vector3(side*.02f,.06f,.04f),new Vector3(side*.037f,.09f,.04f),.003f,accent);
                GearMotif(b,p.set,new Vector3(0,.07f,.047f),.03f,accent);
                var q=Quaternion.Euler(90,0,0);for(int i=0;i<b.V.Count;i++)b.V[i]=q*b.V[i];
            }
            return b.Finish("Equipment "+id+" "+slot+" "+p.rarity+" "+p.set+" "+full);
        }
    }
}
