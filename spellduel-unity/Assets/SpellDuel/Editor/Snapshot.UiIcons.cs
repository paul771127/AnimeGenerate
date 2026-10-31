#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        public static void RenderUiIcons()
        {
            const string dir="snapshots/ui-icons";Directory.CreateDirectory(dir);
            var ids=new List<string>(Skills.All.Keys);ids.Sort(StringComparer.Ordinal);
            int cached=0;
            foreach(string id in ids)
            {
                if(SkillIcons.Get(id)!=SkillIcons.Get(id))throw new Exception("Cache miss: "+id);
                cached++;
                foreach(int size in new[]{56,128})
                {
                    var a=SkillIcons.Render(id,size);var b=SkillIcons.Render(id,size);
                    if(a.Length!=size*size)throw new Exception("Bad dimensions: "+id);
                    for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))throw new Exception("Nondeterministic icon: "+id);
                    var t=new Texture2D(size,size,TextureFormat.RGBA32,false);t.SetPixels32(a);t.Apply();
                    File.WriteAllBytes(Path.Combine(dir,id+"_"+size+".png"),t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
                }
            }
            foreach(PassiveKind kind in Enum.GetValues(typeof(PassiveKind)))
            {
                if(SkillIcons.GetPassive(kind,"mage")!=SkillIcons.GetPassive(kind,"mage"))throw new Exception("Passive cache miss");
                var t=new Texture2D(56,56,TextureFormat.RGBA32,false);t.SetPixels32(SkillIcons.RenderPassive(kind,"mage",56));t.Apply();
                File.WriteAllBytes(Path.Combine(dir,"passive_"+kind+"_56.png"),t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
            }
            File.WriteAllText(Path.Combine(dir,"audit.json"),"{\"unity\":\""+Application.unityVersion+"\",\"icons\":"+ids.Count+",\"cacheHits\":"+cached+",\"sizes\":[56,128],\"deterministic\":true}");
        }
    }
}
#endif
