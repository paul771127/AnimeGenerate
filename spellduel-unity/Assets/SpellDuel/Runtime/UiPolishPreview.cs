#if UNITY_EDITOR || UI_POLISH_PREVIEW
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace SpellDuel
{
    // Only included in the dedicated Linux visual fixture, never a mobile player.
    public sealed class UiPolishPreview : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        SoloBattle solo;Camera view;Font font;int page;bool failed;string output;
        CharacterProfile profile;CharacterPanel panel;WorldUI world;GameRoot home;
        readonly string[] pages={"battle_light","battle_dark","status_light","status_dark","explore_dialog","dungeon_party","story_party","charge_cards","home","characters","prep","stats","tree","equipment","inventory","pets","map","town","smith","shop","guild","craft","alchemy","trialshop","dungeons","tower","settings","victory","defeat","tip","guide","loading","rewards","town_flame","town_forest","town_harbor","town_desert","town_snow"};
        void Set(string name,object value)=>typeof(SoloBattle).GetField(name,Flags).SetValue(solo,value);
        object Call(string name,params object[] args)=>typeof(SoloBattle).GetMethod(name,Flags).Invoke(solo,args);
        IEnumerator Start()
        {
            Application.runInBackground=true;Debug.Log("PREVIEW start");
            output=Environment.GetEnvironmentVariable("UI_PREVIEW_OUTPUT")??"ui-preview";Directory.CreateDirectory(output);
            Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Exception||type==LogType.Error){failed=true;Debug.LogWarning("PREVIEW FAILURE: "+message);}};
            Screen.SetResolution(1080,2340,false);QualitySettings.vSyncCount=0;Application.targetFrameRate=30;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Noto Sans CJK TC","Noto Sans CJK SC","DejaVu Sans"},36);
            view=new GameObject("Fixture camera").AddComponent<Camera>();view.clearFlags=CameraClearFlags.SolidColor;
            view.transform.position=new Vector3(0,1.6f,-5);view.transform.rotation=Quaternion.identity;
            solo=new GameObject("Fixture HUD").AddComponent<SoloBattle>();solo.enabled=false;
            Set("cam",view);Set("myClass","mage");Set("voice",VoiceTemplates.Load());Set("dummyMode",true);
            typeof(SoloBattle).GetProperty("phase",Flags).SetValue(solo,SoloBattle.Phase.Fighting);
            CastGuidePref.Set(false);
            profile=new CharacterProfile{cls="mage",level=35,adv="archmage",novicePast=true,statPoints=8,skillPoints=14};
            Set("prof",profile);panel=new CharacterPanel();world=new WorldUI();
            for(int rarity=0;rarity<6;rarity++)Progress.Bag.Add(new Item{name=Item.RarityNames[rarity]+"・星紋法杖",slot=Slot.Weapon,rarity=rarity,uid=100+rarity,ilvl=20});
            profile.equipped[0]=Progress.Bag[Progress.Bag.Count-1];
            home=FindObjectOfType<GameRoot>();typeof(GameRoot).GetField("solo",Flags).SetValue(home,solo);
            var loadout=(List<string>)typeof(SoloBattle).GetField("myLoadout",Flags).GetValue(solo);loadout.AddRange(new[]{"fire","ice","wind"});
            Debug.Log("PREVIEW fixture configured");
            var ids=new List<string>();foreach(var s in Skills.All.Values)if(s.cls=="mage"&&s.cost>0&&ids.Count<3)ids.Add(s.id);
            var me=new Fighter("預覽玩家",Skills.Classes["mage"],ids,true);
            var enemy=new Fighter("測試敵人",Skills.Classes["swordsman"],new string[0],false);
            enemy.head=new Vector3(0,1.5f,1);enemy.hp=enemy.maxHp*.68f;
            var battle=new Battle(me,enemy);Set("battle",battle);me.hp=me.maxHp*.62f;me.mp=0;
            if(ids.Count>0)me.cooldownUntil[ids[0]]=5f;
            me.blockUntil=12;me.snaredUntil=8;me.silencedUntil=6;
            yield return null;
            for(page=0;page<pages.Length;page++)
            {
                view.backgroundColor=page==1||page==3?new Color(.035f,.04f,.05f):new Color(.88f,.87f,.83f);
                Debug.Log("PREVIEW page "+pages[page]);
                yield return null;yield return new WaitForEndOfFrame();
                var t=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
                t.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);t.Apply();
                if(t.width!=1080||t.height!=2340)throw new Exception("Wrong screenshot size: "+t.width+"x"+t.height);
                File.WriteAllBytes(Path.Combine(output,pages[page]+".png"),t.EncodeToPNG());Destroy(t);
                if(failed)break;
            }
            File.WriteAllText(Path.Combine(output,"native.json"),"{\"unity\":\""+Application.unityVersion+"\",\"nativeIMGUI\":true,\"failed\":"+(failed?"true":"false")+"}");
            Application.Quit(failed?1:0);
        }
        void DrawMenu(string name)
        {
            const float w=1080,h=2340;const int fs=48,small=38;
            if(name.StartsWith("town_"))
            {world.town=Array.IndexOf(new[]{"town_flame","town_forest","town_harbor","town_desert","town_snow"},name);world.screen=WorldUI.Screen.Town;world.Draw(profile);return;}
            if(name=="victory")
            {
                Set("dlg",null);var type=typeof(SoloBattle).GetNestedType("VictoryInfo",Flags);var v=Activator.CreateInstance(type,true);
                void V(string key,object value)=>type.GetField(key,Flags).SetValue(v,value);
                V("title","迷霧森林・通關");V("rank","S");V("time",142f);V("par",180f);V("hpFrac",.82f);V("gold",280);V("xp",900);V("ups",2);V("note","升級！可以分配能力點與技能點。");
                ((List<Item>)type.GetField("items",Flags).GetValue(v)).AddRange(Progress.Bag);Set("victory",v);
                Call("DrawVictory",w,h,32f,40f,2300f,16f,72f,fs,small);return;
            }
            if(name=="defeat")
            {
                Set("victory",null);typeof(SoloBattle).GetProperty("phase",Flags).SetValue(solo,SoloBattle.Phase.Over);Call("DrawHud");return;
            }
            if(name=="tip")
            {Set("tipId","preview");Set("tipTitle","整理裝備與技能");Set("tipText","選擇裝備後查看能力比較，再確認穿上。\n學會新技能後，記得把它放進技能欄。");Call("DrawTip");return;}
            if(name=="guide")
            {typeof(SoloBattle).GetProperty("phase",Flags).SetValue(solo,SoloBattle.Phase.Setup);typeof(SoloBattle).GetProperty("GuideStep",Flags).SetValue(solo,0);Set("storyMapOpen",true);Call("DrawGuide");return;}
            if(name=="loading")
            {
                var video=GetComponent<StoryVideo>();if(!video){video=gameObject.AddComponent<StoryVideo>();video.enabled=false;}
                var field=typeof(StoryVideo).GetField("state",Flags);field.SetValue(video,Enum.Parse(field.FieldType,"Preloading"));typeof(StoryVideo).GetMethod("Draw",Flags).Invoke(video,null);return;
            }
            if(name=="rewards")
            {
                string[] labels={"金幣 +280","材料：風暴寶石","🎁 [神話] 星紋法杖","💠 古根精魄","配方：天穹護符","⬆ Lv 36"};
                var list=(System.Collections.IList)typeof(SoloBattle).GetField("floaters",Flags).GetValue(solo);list.Clear();
                var type=typeof(SoloBattle).GetNestedType("Floater",Flags);
                for(int i=0;i<labels.Length;i++)
                {
                    var f=Activator.CreateInstance(type,true);type.GetField("text",Flags).SetValue(f,labels[i]);type.GetField("born",Flags).SetValue(f,Time.time);
                    type.GetField("color",Flags).SetValue(f,i==2?Item.RarityColors[5]:i==1?new Color(.5f,.85f,1):i==3?new Color(.8f,.6f,1):UiKit.Gold);
                    type.GetField("posMap",Flags).SetValue(f,view.ScreenToWorldPoint(new Vector3(540,1900-i*220,3)));list.Add(f);
                }
                typeof(SoloBattle).GetProperty("phase",Flags).SetValue(solo,SoloBattle.Phase.Fighting);Call("DrawHud");
                return;
            }
            if(name=="home"||name=="characters")
            {typeof(GameRoot).GetMethod(name=="home"?"DrawHome":"DrawCharacterSelect",Flags).Invoke(home,new object[]{w,h,32f,32f,77f,fs,small});return;}
            if(name=="prep"){Call("DrawPrep");return;}
            var names=new[]{"stats","tree","equipment","inventory","pets"};int tab=Array.IndexOf(names,name);
            if(tab>=0){panel.tab=tab;panel.Draw(new Rect(32,56,1016,2228),profile);return;}
            if(name=="settings")
            {SettingsMenu.Show();SettingsMenu.I.enabled=false;typeof(SettingsMenu).GetMethod("OnGUI",Flags).Invoke(SettingsMenu.I,null);return;}
            WorldUI.Screen screen=WorldUI.Screen.Map;
            switch(name)
            {
                case "town":screen=WorldUI.Screen.Town;break;case "smith":screen=WorldUI.Screen.Smith;break;
                case "shop":screen=WorldUI.Screen.Shop;break;case "guild":screen=WorldUI.Screen.Guild;break;
                case "craft":screen=WorldUI.Screen.Craft;break;case "alchemy":screen=WorldUI.Screen.Alchemy;break;
                case "trialshop":screen=WorldUI.Screen.TrialShop;break;case "dungeons":screen=WorldUI.Screen.Dungeon;break;
                case "tower":screen=WorldUI.Screen.Tower;break;
            }
            world.screen=screen;world.Draw(profile);
        }
        void OnGUI()
        {
            if(solo==null||page>=pages.Length)return;
            GUI.skin.font=font;
            try
            {
                if(page>=8){DrawMenu(pages[page]);return;}
                if(page<4){Call("DrawHud");return;}
                if(page==4)
                {
                    // The production dialog drawer, with an explicit static fixture.
                    var map=new ExploreMap(9,9);var run=new ExploreRun(map);
                    run.talking=new ExNpc{name="艾琳"};run.talkText="晨鐘還沒有沉默。沿著發光的小徑走，我們會找到森林的出口。";
                    Set("exploreRun",run);
                    // Draw the production minimap and opaque NPC dialogue.
                    Call("DrawExploreMinimap",run,1080f,2340f);Call("DrawExploreDialog");
                    return;
                }
                if(page==5||page==6)
                {
                    Set("exploreRun",null);
                    object[] args={240f,32f,16f,72f,48,38,1080f};
                    Call(page==5?"DrawDungeonParty":"DrawStoryParty",args);return;
                }
                for(int i=0;i<6;i++)
                {
                    var r=new Rect(60+(i%2)*490,180+(i/2)*620,450,550);
                    UiKit.Card(r,.95f,Skills.Classes["mage"].color);
                    Call("DrawCastingTile",r,Skills.Classes["mage"].color,38);
                }
            }
            catch(Exception e){failed=true;Debug.LogException(e);}
        }
    }
}
#endif

#if UI_POLISH_PREVIEW && !UNITY_EDITOR && !UNITY_ANDROID && !UNITY_IOS
// Linux screenshot fixtures have no phone vibrator. Mobile builds use UnityEngine.Handheld unchanged.
namespace SpellDuel { internal static class Handheld { public static void Vibrate() {} } }
#endif
