using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public sealed class StageWeatherArt : MonoBehaviour
    {
        public Battle.Weather Weather {get;private set;}
        readonly List<Mesh> meshes=new List<Mesh>();
        Material moonMat;
        public static StageWeatherArt Create(Battle.Weather weather,List<Vector3> polygon,float y)
        {
            var art=new GameObject("StageWeather_"+weather).AddComponent<StageWeatherArt>();art.Weather=weather;
            ArtKit.CenterAndRadius(polygon,y,out var center,out float radius);art.transform.position=center;
            ParticleSystem Particles(string name,int count,Vector3 pos,Color color,float size,float life,float rate)
            {
                var ps=SpellFx.NewPS(art.transform,name,SpellFx.BlendMat(SpellFx.TexSoft),count,false);ps.transform.localPosition=pos;
                var m=ps.main;m.startColor=color;m.startSize=size;m.startLifetime=life;m.startSpeed=0;
                var e=ps.emission;e.rateOverTime=rate;return ps;
            }
            if(weather==Battle.Weather.Rain)
            {
                var ps=Particles("Falling rain",360,Vector3.up*4,new Color(.65f,.83f,1,.38f),.025f,.5f,500);
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(radius*2,.01f,radius*2);
                var vel=ps.velocityOverLifetime;vel.enabled=true;vel.x=.7f;vel.y=-8;
                var r=ps.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=5;r.velocityScale=.015f;ps.Play();
                var splash=Particles("Ground droplets",120,Vector3.up*.025f,new Color(.75f,.9f,1,.5f),.045f,.18f,150);
                shape=splash.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(radius*2,.001f,radius*2);
                var sm=splash.main;sm.startSpeed=.3f;sm.gravityModifier=.25f;splash.Play();
                // Low-opacity surface glints remain below red telegraphs; no extra reflection camera.
                var glints=Particles("Wet floor sheen",24,Vector3.up*.014f,new Color(.55f,.8f,1,.1f),.8f,1,15);
                shape=glints.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(radius*2,.001f,radius*2);
                var gr=glints.GetComponent<ParticleSystemRenderer>();gr.renderMode=ParticleSystemRenderMode.HorizontalBillboard;glints.Play();
            }
            else if(weather==Battle.Weather.Fog)
            {
                // Concentric rings make opacity accumulate with distance, leaving combat readable.
                for(int ring=0;ring<3;ring++)
                {
                    var fog=Particles("Distant mist "+ring,40,Vector3.up*(1.2f+ring*.3f),new Color(.7f,.81f,.86f,.32f+ring*.08f),6+ring*1.5f,8,5);
                    SpellFx.Disc(fog,radius+3+ring*4,.05f);fog.transform.localRotation=Quaternion.Euler(90,0,0);
                    var noise=fog.noise;noise.enabled=true;noise.strength=.2f;noise.frequency=.1f;SpellFx.FadeInOut(fog);fog.Play();
                }
            }
            else if(weather==Battle.Weather.Wind)
            {
                var leaves=Particles("Wind leaves",48,new Vector3(-radius-1,1.5f,0),new Color(.53f,.86f,.3f,.8f),.22f,4,10);
                var shape=leaves.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.2f,2,radius*2);
                var vel=leaves.velocityOverLifetime;vel.enabled=true;vel.x=2;vel.y=.05f;
                var noise=leaves.noise;noise.enabled=true;noise.strength=.35f;noise.frequency=.45f;
                var spin=leaves.rotationOverLifetime;spin.enabled=true;spin.z=2;
                var mb=new ArtKit.MB(1);ArtKit.QuadToward(mb,0,new Vector3(-.7f,0,0),new Vector3(0,.25f,.05f),new Vector3(.7f,0,0),new Vector3(0,-.25f,.05f),Vector3.forward);
                var mesh=mb.Build("Leaf silhouette");art.meshes.Add(mesh);
                var lr=leaves.GetComponent<ParticleSystemRenderer>();lr.renderMode=ParticleSystemRenderMode.Mesh;lr.mesh=mesh;lr.sharedMaterial=SpellFx.BlendMat(SpellFx.TexLeaf);leaves.Play();
            }
            else if(weather==Battle.Weather.Moon)
            {
                var moon=new GameObject("Large moon");moon.transform.SetParent(art.transform,false);moon.transform.localPosition=new Vector3(8,12,18);moon.transform.localRotation=Quaternion.Euler(90,0,0);
                var mesh=ArtKit.FlatDisc(2.2f,64,0,"Moon disc");art.meshes.Add(mesh);moon.AddComponent<MeshFilter>().sharedMesh=mesh;
                art.moonMat=new Material(Shader.Find("Sprites/Default")){color=new Color(.79f,.88f,1,.85f)};moon.AddComponent<MeshRenderer>().sharedMaterial=art.moonMat;
                var stars=Particles("Moon dust",48,Vector3.up*6,new Color(.6f,.75f,1,.4f),.035f,5,5);SpellFx.Sphere(stars,radius+4);stars.Play();
            }
            return art;
        }
        /// <summary>副本的夜晚：場地上空的星點、場邊飄的螢光（不擋戰鬥畫面）</summary>
        public static StageWeatherArt CreateNight(List<Vector3> polygon,float y)
        {
            var art=new GameObject("DungeonNight").AddComponent<StageWeatherArt>();art.Weather=Battle.Weather.None;
            ArtKit.CenterAndRadius(polygon,y,out var center,out float radius);art.transform.position=center;
            var stars=SpellFx.NewPS(art.transform,"Night stars",SpellFx.BlendMat(SpellFx.TexSoft),160,false);stars.transform.localPosition=Vector3.up*9;
            var m=stars.main;m.startColor=new Color(.8f,.88f,1,.75f);m.startSize=.06f;m.startLifetime=6;m.startSpeed=0;var e=stars.emission;e.rateOverTime=25;
            SpellFx.Sphere(stars,radius+10);SpellFx.FadeInOut(stars);stars.Play();
            var flies=SpellFx.NewPS(art.transform,"Night glowflies",SpellFx.AddMat(SpellFx.TexSoft),40,false);flies.transform.localPosition=Vector3.up*.8f;
            m=flies.main;m.startColor=new Color(1,.9f,.5f,.8f);m.startSize=.05f;m.startLifetime=5;m.startSpeed=0;e=flies.emission;e.rateOverTime=7;
            SpellFx.Disc(flies,radius+1.5f,.4f);var noise=flies.noise;noise.enabled=true;noise.strength=.3f;noise.frequency=.4f;SpellFx.FadeInOut(flies);flies.Play();
            return art;
        }
        void OnDestroy(){foreach(var m in meshes)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}if(moonMat){if(Application.isPlaying)Destroy(moonMat);else DestroyImmediate(moonMat);}}
    }
}
