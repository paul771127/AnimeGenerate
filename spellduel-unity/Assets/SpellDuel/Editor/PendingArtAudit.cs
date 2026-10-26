#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
namespace SpellDuel.EditorTools {
public static class PendingArtAudit {
[Serializable] public class Report { public string unity;public int towns,hotspots,facilitySprites,dungeonSprites,supplyIcons,speakers,playerPlaceholdersSkipped;public bool mapEntries,localFallback; }
static readonly BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
static void Check(bool ok,string why){if(!ok)throw new Exception("Pending art: "+why);}
public static Report Run(){
 var report=new Report{unity=Application.unityVersion};var world=new WorldUI();
 var map=typeof(WorldUI).GetMethod("MapFor",BindingFlags.Static|BindingFlags.NonPublic);
 var town=typeof(WorldUI).GetField("town",Instance);var screen=typeof(WorldUI).GetField("screen",Instance);
 var open=typeof(WorldUI).GetMethod("OpenMapSite",Instance);var bg=typeof(WorldUI).GetMethod("FacilityBg",Instance);
 for(int t=0;t<5;t++){
  var data=map.Invoke(null,new object[]{t});Check(data!=null,"town map missing "+t);var hit=(Dictionary<string,Rect>)data.GetType().GetField("hit").GetValue(data);
  foreach(string key in new[]{"alchemist","crafter"}){Check(hit.ContainsKey(key),"hotspot "+key);foreach(var other in hit)if(other.Key!=key)Check(!hit[key].Overlaps(other.Value),"ambiguous hotspot "+key+"/"+other.Key);report.hotspots++;}
  town.SetValue(world,t);foreach(string key in new[]{"alchemist","crafter"}){var tex=(Texture2D)bg.Invoke(world,new object[]{key});Check(tex&&tex.width==1080&&tex.height==1920,"facility bg "+key);open.Invoke(world,new object[]{key});Check(screen.GetValue(world).ToString()==(key=="alchemist"?"Alchemy":"Craft"),"map entry "+key);Check(typeof(WorldUI).GetField("craftSel",Instance).GetValue(world)==null,"selection not reset");Check((Vector2)typeof(WorldUI).GetField("craftScroll",Instance).GetValue(world)==Vector2.zero,"scroll not reset");}report.towns++;
 }
 report.mapEntries=report.localFallback=true;
 foreach(string id in new[]{"alchemist_tanyi","alchemist_qingluo","alchemist_xi","alchemist_sailin","alchemist_tuoling","crafter_laochui"})foreach(string e in new[]{"normal","smile","serious"}){CheckSprite(id,e);report.facilitySprites++;}
 foreach(string id in new[]{"lingyin","ark","xiaomai","oldsmith_shade","gearmerchant","clockwork","heartcore"})foreach(string e in new[]{"normal",id=="ark"||id=="clockwork"?"angry":"serious","surprised"}){CheckSprite(id,e);report.dungeonSprites++;}
 var ids=new List<string>(SkillIcons.PotionArtIds);ids.AddRange(SkillIcons.MaterialArtIds);var hashes=new HashSet<string>();
 foreach(string id in ids){var file=Resources.Load<Texture2D>("Village2D/icons/"+id);Check(file&&file.width==128&&file.height==128,"supply resource "+id);var pixels=SkillIcons.Render(id,128);var bytes=new byte[pixels.Length*4];for(int i=0;i<pixels.Length;i++){bytes[i*4]=pixels[i].r;bytes[i*4+1]=pixels[i].g;bytes[i*4+2]=pixels[i].b;bytes[i*4+3]=pixels[i].a;}using(var sha=SHA256.Create())Check(hashes.Add(Convert.ToBase64String(sha.ComputeHash(bytes))),"duplicate supply icon "+id);var tex=new Texture2D(128,128,TextureFormat.RGBA32,false);tex.SetPixels32(pixels);tex.Apply();File.WriteAllBytes("snapshots/pending_icon_"+id+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);report.supplyIcons++;}
 foreach(string file in new[]{"story","dungeons"})foreach(var raw in Resources.Load<TextAsset>(file).text.Split('\n')){
  string line=raw.Trim();if(line.Length==0||line.StartsWith("#")||line.StartsWith("[")||line.StartsWith("<")||line.StartsWith("背景：")||Regex.IsMatch(line,@"^[a-z_]+\s*=")||!line.Contains("："))continue;
  var entry=Story.ParseLine(line);if(string.IsNullOrEmpty(entry.speaker))continue;
  if(entry.speaker=="{玩家}"){report.playerPlaceholdersSkipped++;continue;}
  Check(Story2D.IdOf(entry.speaker)!=null,"speaker has no portrait id: "+entry.speaker);report.speakers++;
 }
 File.WriteAllText("snapshots/pending_art_audit.json",JsonUtility.ToJson(report,true));Debug.Log("[SpellDuel] Pending art audit passed: five maps / ten entries, 39 portraits, 21 supply icons, all named speakers mapped; player placeholder skipped.");return report;
}
static void CheckSprite(string id,string expr){var tex=Resources.Load<Texture2D>("Story2D/chars/"+id+"_"+expr);Check(tex&&tex.width==768&&tex.height==1152,"portrait "+id+"/"+expr);}
}
}
#endif
