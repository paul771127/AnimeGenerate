#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static class AreaEnemyPreview
    {
        [MenuItem("SpellDuel/Art/Render new area enemies")]
        public static void Render()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/minions"));
            Directory.CreateDirectory(output);
            var stage=new GameObject("Area enemy preview stage");
            var cameraObject=new GameObject("Art camera");
            var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.55f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.66f,.76f,.79f);
            var lightObject=new GameObject("Art key light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;
            lightObject.transform.rotation=Quaternion.Euler(40,-35,0);
            var target=new RenderTexture(500,350,24);camera.targetTexture=target;
            var sheet=new Texture2D(1500,1400,TextureFormat.RGB24,false);
            var tick=typeof(MinionRig).GetMethod("Animate",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=RenderTexture.active;
            try
            {
                for(int k=19;k<=22;k++)
                {
                    var rig=MinionRig.Create();rig.transform.SetParent(stage.transform,false);rig.SetVariant(k);
                    rig.SetDress(k==19?1:k==20?2:k==21?3:4);
                    for(int view=0;view<3;view++)
                    {
                        if(view==2){rig.Windup(1.5f);tick.Invoke(rig,new object[]{1.5f});}
                        else tick.Invoke(rig,new object[]{0f});
                        camera.transform.position=view==0?new Vector3(0,1.5f,6):view==1?new Vector3(6,1.5f,0):new Vector3(3.5f,2.2f,5);
                        camera.transform.LookAt(new Vector3(0,.9f,0));camera.Render();RenderTexture.active=target;
                        sheet.ReadPixels(new Rect(0,0,500,350),view*500,(22-k)*350);
                    }
                    Object.DestroyImmediate(rig.gameObject);
                }
                sheet.Apply();File.WriteAllBytes(Path.Combine(output,"new_enemies.jpg"),sheet.EncodeToJPG(92));
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;
                Object.DestroyImmediate(target);Object.DestroyImmediate(sheet);Object.DestroyImmediate(stage);
                Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lightObject);
            }
        }
    }
}
#endif
