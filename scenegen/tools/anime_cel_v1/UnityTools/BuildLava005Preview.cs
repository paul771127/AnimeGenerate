using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class BuildLava005Preview
{
    [Serializable] public class BoundsRecord { public float[] min, max; }
    [Serializable] public class Part { public string id, variant, file; public int triangles, triangleBudget; public BoundsRecord bounds; }
    [Serializable] public class Kit { public Part[] parts; }
    const string AssetsDir = "Assets/Lava005";
    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, name);
        if(i < 0 || i + 1 >= a.Length) throw new Exception("Missing " + name);
        return Path.GetFullPath(a[i + 1]);
    }
    static void Check(bool condition, string message) { if(!condition) throw new Exception(message); }
    static void Copy(string source, string asset)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(asset));
        File.Copy(source, asset, true);
        AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
    }
    static void Texture(string path, int maxSize, bool data=false)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.sRGBTexture = !data; imp.mipmapEnabled = true; imp.isReadable = false;
        imp.maxTextureSize = maxSize; imp.textureCompression = TextureImporterCompression.Compressed;
        imp.wrapMode = TextureWrapMode.Repeat; imp.filterMode = FilterMode.Trilinear;
        foreach(string platform in new[]{"Android", "iPhone"})
        {
            var p = imp.GetPlatformTextureSettings(platform); p.name=platform; p.overridden=true;
            p.maxTextureSize=maxSize; p.format=TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(p);
        }
        imp.SaveAndReimport();
    }
    static void Model(string path)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        imp.animationType = ModelImporterAnimationType.None;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.importNormals = ModelImporterNormals.Import;
        imp.isReadable = false; imp.SaveAndReimport();
    }
    static Material TexMat(string name, string texture)
    {
        var m = new Material(Shader.Find("Unlit/Texture")); m.name=name;
        m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsDir+"/Kit/"+texture);
        Check(m.mainTexture != null, "Missing texture "+texture);
        AssetDatabase.CreateAsset(m,AssetsDir+"/Materials/"+name+".mat"); return m;
    }
    static Material ColorMat(string name, Color color)
    {
        var m=new Material(Shader.Find("Unlit/Color")); m.color=color;
        AssetDatabase.CreateAsset(m,AssetsDir+"/Materials/"+name+".mat"); return m;
    }
    static void CameraPose(Camera cam, Vector3 position, Vector3 target, float horizontalFov)
    {
        cam.transform.position=position; cam.transform.LookAt(target);
        cam.aspect=1600f/900f;
        cam.fieldOfView=2f*Mathf.Atan(Mathf.Tan(horizontalFov*Mathf.Deg2Rad*.5f)/cam.aspect)*Mathf.Rad2Deg;
    }
    static void Render(Camera cam, string path)
    {
        var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        rt.antiAliasing=2; cam.targetTexture=rt; cam.Render();
        var old=RenderTexture.active; RenderTexture.active=rt;
        var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,1600,900),0,0); tex.Apply();
        File.WriteAllBytes(path,tex.EncodeToPNG()); cam.targetTexture=null;
        RenderTexture.active=old; Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
    }
    static Color32[] Capture(Camera camera)
    {
        var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(256,256,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();var pixels=image.GetPixels32();camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);return pixels;
    }
    static int FlowMotionTest(Camera cam,GameObject level,Material flow,string dir)
    {
        var layers=new Dictionary<GameObject,int>();foreach(var tr in level.GetComponentsInChildren<Transform>(true)){layers[tr.gameObject]=tr.gameObject.layer;tr.gameObject.layer=8;if(tr.name.StartsWith("lava_flow"))tr.gameObject.layer=9;}
        var first=level.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("lava_flow")).OrderBy(r=>r.bounds.center.x).First();var c=first.bounds.center;
        int oldMask=cam.cullingMask;Color oldColor=cam.backgroundColor;cam.cullingMask=1<<9;cam.backgroundColor=Color.black;cam.orthographic=true;cam.orthographicSize=3;
        CameraPose(cam,c+Vector3.right*5+Vector3.up*.2f,c,76);cam.aspect=1;
        flow.SetFloat("_PreviewTime",0);var a=Capture(cam);flow.SetFloat("_PreviewTime",.9f);var b=Capture(cam);int changed=0;
        for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>6)changed++;
        var da=new Texture2D(256,256,TextureFormat.RGB24,false);da.SetPixels32(a);da.Apply();File.WriteAllBytes(Path.Combine(dir,"flow_t0.png"),da.EncodeToPNG());da.SetPixels32(b);da.Apply();File.WriteAllBytes(Path.Combine(dir,"flow_t1.png"),da.EncodeToPNG());Object.DestroyImmediate(da);
        Debug.Log("FLOW_DIAG name="+first.name+" centre="+c+" changed="+changed);
        foreach(var item in layers)item.Key.layer=item.Value;cam.cullingMask=oldMask;cam.backgroundColor=oldColor;cam.orthographic=false;flow.SetFloat("_PreviewTime",-1);
        Check(changed>50,"UV scroll produced no visible movement");return changed;
    }
    public static void Execute()
    {
        string run=Arg("--cel-run"), report=Arg("--cel-report");
        var logs=new List<string>();
        try
        {
            Check(GraphicsSettings.currentRenderPipeline==null,"Preview project must use Built-in");
            PlayerSettings.colorSpace=ColorSpace.Linear;
            Directory.CreateDirectory(AssetsDir+"/Kit"); Directory.CreateDirectory(AssetsDir+"/Demo"); Directory.CreateDirectory(AssetsDir+"/Materials");
            AssetDatabase.Refresh();
            foreach(var p in Directory.GetFiles(Path.Combine(run,"fbx"),"*.fbx"))
            {
                string target=AssetsDir+"/Kit/"+Path.GetFileName(p); Copy(p,target); Model(target);
            }
            foreach(var name in new[]{"volcano_cel_v2_albedo.png","volcano_cel_v2_backdrop_albedo.png","volcano_cel_v2_emissive.png","volcano_cel_v2_backdrop_emissive.png","cel_ground_ash.png","cel_ground_crust.png"})
            {
                string target=AssetsDir+"/Kit/"+name; Copy(Path.Combine(run,name),target);
                int max=name.Contains("emissive")?512:name.Contains("backdrop")?1024:name.Contains("albedo")?2048:512;
                Texture(target,max,name.Contains("emissive"));
            }
            Kit kit=JsonUtility.FromJson<Kit>(File.ReadAllText(Path.Combine(run,"kit.json")));
            int total=0;
            foreach(Part part in kit.parts)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetsDir+"/Kit/"+part.file);
                Check(prefab!=null,"Missing FBX "+part.file);
                // Model mesh access for inspection uses the imported mesh asset, without enabling Read/Write.
                int triangles=0;
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    for(int s=0;s<filter.sharedMesh.subMeshCount;s++) triangles+=(int)filter.sharedMesh.GetIndexCount(s)/3;
                // Unity can discard zero-area triangles when importing the decimated FBX meshes.
                Check(triangles==part.triangles,part.file+" unexpected triangle change "+triangles+" / "+part.triangles);
                Check(triangles<=part.triangleBudget,part.file+" over budget");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var renderers=go.GetComponentsInChildren<Renderer>(true);
                Bounds b=renderers[0].bounds; foreach(var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
                Vector3 expectedMin=new Vector3(part.bounds.min[0],part.bounds.min[1],part.bounds.min[2]);
                Vector3 expectedMax=new Vector3(part.bounds.max[0],part.bounds.max[1],part.bounds.max[2]);
                Check(Vector3.Distance(b.min,expectedMin)<.025f && Vector3.Distance(b.max,expectedMax)<.025f,part.file+" imported bounds mismatch: "+b.min+" "+b.max);
                if(part.id=="gate")
                {
                    var names=go.GetComponentsInChildren<Transform>(true).Select(x=>x.name).ToArray();
                    Check(names.Contains("gate_frame")&&names.Contains("gate_door"),"Gate component names lost");
                }
                if(part.id=="wall_lavafall")
                {
                    var child=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="lava_flow");
                    Check(child!=null,"Missing lava_flow child");
                    Check(child.GetComponent<Renderer>().bounds.max.z<=.6001f,"Flow pool extends more than 0.6m");
                }
                logs.Add(part.file+": Unity="+triangles+", FBX="+part.triangles+", budget="+part.triangleBudget+", Y-up bounds/pivot passed"); total+=triangles; Object.DestroyImmediate(go);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);

            var main=TexMat("CelMain","volcano_cel_v2_albedo.png"); var back=TexMat("CelBackdrop","volcano_cel_v2_backdrop_albedo.png");
            Shader terrainShader=Shader.Find("SpellDuel/StageKit"); Check(terrainShader!=null&&!ShaderUtil.ShaderHasError(terrainShader),"Game terrain shader failed");
            main.shader=terrainShader;main.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsDir+"/Kit/volcano_cel_v2_emissive.png"));main.SetFloat("_Glow",1.8f);main.SetFloat("_UnderGlowStrength",.55f);main.SetColor("_Haze",new Color(.72f,.61f,.56f).linear);
            back.shader=terrainShader;back.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsDir+"/Kit/volcano_cel_v2_backdrop_emissive.png"));back.SetFloat("_Glow",.65f);back.SetFloat("_HazeNear",25);back.SetFloat("_HazeFar",90);back.SetFloat("_HazeMax",.55f);back.SetColor("_Haze",new Color(.72f,.61f,.56f).linear);
            var flow=TexMat("LavaFlow","volcano_cel_v2_albedo.png");flow.shader=Shader.Find("SceneKit/LavaFlow005");var diag=AssetDatabase.LoadAssetAtPath<Shader>("Assets/PreviewTools/LavaFlow005.shader");if(diag!=null)foreach(var msg in ShaderUtil.GetShaderMessages(diag))Debug.LogError("FLOW_SHADER: "+msg.message+" line="+msg.line);Check(flow.shader!=null&&!ShaderUtil.ShaderHasError(flow.shader),"Flow shader failed");flow.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsDir+"/Kit/volcano_cel_v2_emissive.png"));flow.SetFloat("_PreviewTime",0);flow.enableInstancing=true;
            var ash=TexMat("CelAsh","cel_ground_ash.png"); var crust=TexMat("CelCrust","cel_ground_crust.png"); var warm=back;
            var lava=ColorMat("LavaCore",new Color(1,.29f,.035f)); var edge=ColorMat("LavaOuter",new Color(.62f,.09f,.012f));
            var dark=ColorMat("CoolingBank",new Color(.025f,.045f,.055f));
            Shader skyShader=Shader.Find("SceneKit/CelLava005Sky"); Check(skyShader!=null&&!ShaderUtil.ShaderHasError(skyShader),"Vertex color shader failed");
            var sky=new Material(skyShader); AssetDatabase.CreateAsset(sky,AssetsDir+"/Materials/Sky.mat");
            string demo=AssetsDir+"/Demo/level.fbx"; Copy(Path.Combine(run,"level.fbx"),demo); Model(demo);
            var level=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(demo)); level.name="AnimeCelVolcano";
            foreach(var renderer in level.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n=renderer.name;
                Material m=main;
                if(n.StartsWith("lava_flow")) m=flow;
                else if(n.StartsWith("backdrop")) m=back;
                else if(n.StartsWith("COL_CelFloor")) m=ash;
                else if(n.StartsWith("VIS_CoolingCrust")) m=crust;
                else if(n.StartsWith("VIS_LavaRiver")) m=edge;
                else if(n.StartsWith("VIS_LavaHotCore")||n.StartsWith("VIS_CalderaGlow")) m=lava;
                else if(n.StartsWith("VIS_ContactPatch")) m=dark;
                else if(n.StartsWith("VIS_DistantCaldera")) m=warm;
                else if(n.StartsWith("ENV_CelSkyDome")) m=sky;
                renderer.sharedMaterial=m;
            }
            var cam=new GameObject("PreviewCamera").AddComponent<Camera>(); cam.tag="MainCamera";
            cam.nearClipPlane=.05f; cam.farClipPlane=220; cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.085f,.19f,.21f);
            cam.allowHDR=false; cam.allowMSAA=true;
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            CameraPose(cam,new Vector3(0,1.5f,-3),new Vector3(.1f,1.5f,17),76);
            Render(cam,Path.Combine(Path.GetDirectoryName(report),"unity_player_view.png"));
            CameraPose(cam,new Vector3(2.2f,1.5f,16),new Vector3(4.5f,3,38),80);
            Render(cam,Path.Combine(Path.GetDirectoryName(report),"unity_clearing_view.png"));
            CameraPose(cam,new Vector3(-16,12,-10),new Vector3(4,2,25),66);
            Render(cam,Path.Combine(Path.GetDirectoryName(report),"unity_hero_view.png"));
            CameraPose(cam,new Vector3(0,1.5f,-3),new Vector3(.1f,1.5f,17),76);
            int changed=FlowMotionTest(cam,level,flow,Path.GetDirectoryName(report));
            CameraPose(cam,new Vector3(0,1.5f,-3),new Vector3(.1f,1.5f,17),76);
            cam.gameObject.AddComponent<CelPreviewCamera>();
            string scenePath=AssetsDir+"/CelVolcano.unity"; EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
            AssetDatabase.SaveAssets();
            File.WriteAllText(report,"{\"ok\":true,\"engine\":\"Unity "+Application.unityVersion+"\",\"pipeline\":\"Built-in\",\"parts\":"+kit.parts.Length+",\"triangles\":"+total+",\"mobileFpsMeasured\":false,\"defaultMainTextureMax\":2048,\"shaderErrors\":0,\"flowMotionChangedPixels\":"+changed+",\"flowUvAnimationVerified\":true}");
            File.WriteAllLines(Path.ChangeExtension(report,"parts.txt"),logs);
            Debug.Log("CEL_PREVIEW_PASS parts="+kit.parts.Length+" triangles="+total);
        }
        catch(Exception ex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report,"{\"ok\":false,\"error\":\""+ex.ToString().Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","").Replace("\n","\\n")+"\"}");
            Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }
}
