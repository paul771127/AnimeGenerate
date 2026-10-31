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
        readonly string[] pages={"battle_light","battle_dark","status_light","status_dark","explore_dialog","dungeon_party","story_party","charge_cards"};
        void Set(string name,object value)=>typeof(SoloBattle).GetField(name,Flags).SetValue(solo,value);
        object Call(string name,params object[] args)=>typeof(SoloBattle).GetMethod(name,Flags).Invoke(solo,args);
        IEnumerator Start()
        {
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
        void OnGUI()
        {
            if(solo==null||page>=pages.Length)return;
            GUI.skin.font=font;
            try
            {
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
