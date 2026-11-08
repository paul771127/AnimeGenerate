using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
public static class BuildTown007Preview
{
    const string Dir="Assets/Town007";
    static Dictionary<string,object> D(object o){return (Dictionary<string,object>)o;}
    static List<object> A(object o){return (List<object>)o;}
    static float F(object o){return Convert.ToSingle(o,CultureInfo.InvariantCulture);}
    static string S(object o){return (string)o;}
    static float Get(Dictionary<string,object>d,string k,float value=0){return d.ContainsKey(k)?F(d[k]):value;}
    static string Arg(string n){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,n);if(i<0)throw new Exception("Missing "+n);return Path.GetFullPath(a[i+1]);}
    static void Check(bool yes,string msg){if(!yes)throw new Exception(msg);}
    static Vector3 Pos(object value,float y=0){var a=A(value);return new Vector3(F(a[0]),y,F(a[1]));}
    static Vector3 Local(Dictionary<string,object>b,float x,float y,float z){return Pos(b["pos"])+Quaternion.Euler(0,Get(b,"yaw"),0)*new Vector3(x,y,z);}
    static void Copy(string src,string dst){Directory.CreateDirectory(Path.GetDirectoryName(dst));File.Copy(src,dst,true);AssetDatabase.ImportAsset(dst,ImportAssetOptions.ForceSynchronousImport);}
    static void ImportModel(string path)
    {
        var m=(ModelImporter)AssetImporter.GetAtPath(path);m.animationType=ModelImporterAnimationType.None;m.materialImportMode=ModelImporterMaterialImportMode.None;m.importNormals=ModelImporterNormals.Import;m.isReadable=false;m.SaveAndReimport();
    }
    static void ImportTexture(string path,int max)
    {
        var t=(TextureImporter)AssetImporter.GetAtPath(path);t.maxTextureSize=max;t.sRGBTexture=true;t.mipmapEnabled=true;t.isReadable=false;t.wrapMode=TextureWrapMode.Repeat;t.filterMode=FilterMode.Trilinear;t.textureCompression=TextureImporterCompression.Compressed;
        foreach(var name in new[]{"Android","iPhone"}){var p=t.GetPlatformTextureSettings(name);p.name=name;p.overridden=true;p.maxTextureSize=max;p.format=TextureImporterFormat.ASTC_6x6;t.SetPlatformTextureSettings(p);}t.SaveAndReimport();
    }
    static Material Mat(string name,string texture)
    {
        var m=new Material(Shader.Find("Unlit/Texture"));m.name=name;m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"/Kit/"+texture);m.enableInstancing=true;AssetDatabase.CreateAsset(m,Dir+"/Materials/"+name+".mat");return m;
    }
    static int Tris(GameObject p){return p.GetComponentsInChildren<MeshFilter>(true).Sum(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(i=>(int)f.sharedMesh.GetIndexCount(i)/3));}
    static void Apply(GameObject go,Material arch,Material detail,Material back,bool backdrop=false)
    {
        foreach(var f in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var rr=f.GetComponent<MeshRenderer>();if(rr==null)continue;
            var materials=new Material[f.sharedMesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=backdrop?back:i==1?detail:arch;rr.sharedMaterials=materials;
            rr.shadowCastingMode=ShadowCastingMode.Off;rr.receiveShadows=false;
        }
    }
    static GameObject Spawn(Dictionary<string,object>e,Transform parent,Material arch,Material detail,Material back,Dictionary<string,bool>background)
    {
        string file=S(e["file"]);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Kit/"+file);Check(prefab!=null,"Missing "+file);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
        go.name=e.ContainsKey("id")?S(e["id"]):Path.GetFileNameWithoutExtension(file);go.transform.position=Pos(e["pos"],Get(e,"height"));go.transform.rotation=Quaternion.Euler(0,Get(e,"yaw"),0);go.transform.localScale=Vector3.one*Get(e,"scale",1);Apply(go,arch,detail,back,background.ContainsKey(file)&&background[file]);return go;
    }
    static void Pose(Camera c,Vector3 p,Vector3 t,float hfov,int w=1600,int h=900)
    {
        c.transform.position=p;c.transform.LookAt(t);c.aspect=(float)w/h;c.fieldOfView=2*Mathf.Atan(Mathf.Tan(hfov*Mathf.Deg2Rad*.5f)/c.aspect)*Mathf.Rad2Deg;
    }
    static void Render(Camera c,string path,int w=1600,int h=900)
    {
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.antiAliasing=2;c.targetTexture=rt;c.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());c.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
    }
    static bool Free(Vector3 p,float radius=.27f){return !Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.43f,radius,1<<8,QueryTriggerInteraction.Ignore);}
    static int Route(object route,string label)
    {
        var p=A(route);int count=0;
        for(int i=1;i<p.Count;i++)
        {
            Vector3 a=Pos(p[i-1]),b=Pos(p[i]);int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.12f));
            for(int j=0;j<=n;j++){var q=Vector3.Lerp(a,b,(float)j/n);if(!Free(q)){
                var hits=Physics.OverlapCapsule(q+Vector3.up*.35f,q+Vector3.up*1.43f,.27f,1<<8);throw new Exception("Blocked actual FBX route "+label+" at "+q+" by "+string.Join(",",hits.Select(x=>x.name)));}count++;}
        }
        return count;
    }
    public static void Execute()
    {
        string run=Arg("--town-run"),report=Arg("--town-report"),outdir=Path.GetDirectoryName(report);Directory.CreateDirectory(outdir);
        try
        {
            Check(GraphicsSettings.currentRenderPipeline==null,"Built-in project required");PlayerSettings.colorSpace=ColorSpace.Linear;
            Directory.CreateDirectory(Dir+"/Kit");Directory.CreateDirectory(Dir+"/Materials");AssetDatabase.Refresh();
            foreach(var p in Directory.GetFiles(Path.Combine(run,"fbx"),"*.fbx")){string path=Dir+"/Kit/"+Path.GetFileName(p);Copy(p,path);ImportModel(path);}
            foreach(var n in new[]{"town0_arch_albedo.png","town0_detail_albedo.png","town0_backdrop_albedo.png"}){string path=Dir+"/Kit/"+n;Copy(Path.Combine(run,n),path);ImportTexture(path,n.Contains("backdrop")?1024:2048);}
            foreach(var n in new[]{"preview_npc.fbx","preview_sky.fbx"}){Copy(Path.Combine(run,n),Dir+"/"+n);ImportModel(Dir+"/"+n);}
            var kit=D(TownJson007.Parse(File.ReadAllText(Path.Combine(run,"kit.json"))));var layout=D(TownJson007.Parse(File.ReadAllText(Path.Combine(run,"town_layout.json"))));var background=new Dictionary<string,bool>();int unique=0;
            foreach(var value in A(kit["assets"]))
            {
                var a=D(value);string file=S(a["file"]);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Kit/"+file);int tris=Tris(prefab);Check(tris==(int)F(a["triangles"]),"Native triangle mismatch "+file+" "+tris+" / "+a["triangles"]);unique+=tris;background[file]=S(a["atlas"])=="backdrop";
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var arch=Mat("Architecture","town0_arch_albedo.png");var detail=Mat("Details","town0_detail_albedo.png");var back=Mat("Backdrop","town0_backdrop_albedo.png");var root=new GameObject("EmberVillage007");int total=0;
            var all=new List<GameObject>();foreach(string list in new[]{"buildings","props","ground","environment"})foreach(var e in A(layout[list])){var go=Spawn(D(e),root.transform,arch,detail,back,background);all.Add(go);total+=Tris(go);}
            foreach(string name in new[]{"gate","perimeter"}){var go=Spawn(D(layout[name]),root.transform,arch,detail,back,background);all.Add(go);total+=Tris(go);}
            Check(total==(int)F(D(kit["triangles"])["instancedTown"])&&total<=60000,"Instanced count mismatch");
            foreach(var go in all)foreach(var f in go.GetComponentsInChildren<MeshFilter>(true)){f.gameObject.layer=8;var mc=f.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;}
            Physics.SyncTransforms();int samples=0,roomSamples=0;
            foreach(var p in A(layout["paths"]))samples+=Route(p,"exterior");
            foreach(var p in A(layout["interactionRoutes"]))samples+=Route(D(p)["points"],S(D(p)["npc"]));
            foreach(var value in A(layout["buildings"]))
            {
                var b=D(value);if(!b.ContainsKey("enterable")||!(bool)b["enterable"])continue;var size=A(b["clearWalkSize"]);var centre=A(b["clearWalkCenter"]);float w=F(size[0]),d=F(size[1]),cz=F(centre[1]);
                for(int i=0;i<=12;i++)for(int j=0;j<=12;j++){var point=Local(b,-w/2+w*i/12,0,cz-d/2+d*j/12);Check(Free(point),"Actual FBX blocks clear room "+b["id"]+" at "+point);roomSamples++;}
            }
            foreach(var value in A(layout["npcs"]))
            {
                var n=D(value);Check(Free(Pos(n["pos"]),.19f),"NPC intersects actual FBX "+n["role"]);
                var a=Pos(D(n["conversation"])["playerPos"]);Check(Free(a),"Talk position blocked "+n["role"]);float distance=Vector3.Distance(a,Pos(n["pos"]));Check(distance>=1.2f&&distance<=2.2f,"Talk range mismatch");
                var marker=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/preview_npc.fbx"),root.transform);marker.name="NPC_MARKER_"+n["role"];marker.transform.position=Pos(n["pos"],.02f);marker.transform.rotation=Quaternion.Euler(0,F(n["yaw"]),0);Apply(marker,arch,detail,back);
            }
            var sky=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/preview_sky.fbx"),root.transform);var skyShader=Shader.Find("SceneKit/CelLava005Sky");Check(skyShader!=null&&!ShaderUtil.ShaderHasError(skyShader),"Sky shader failed");var skyMat=new Material(skyShader);AssetDatabase.CreateAsset(skyMat,Dir+"/Materials/Sky.mat");foreach(var rr in sky.GetComponentsInChildren<Renderer>())rr.sharedMaterial=skyMat;
            var cam=new GameObject("TownPreviewCamera").AddComponent<Camera>();cam.nearClipPlane=.05f;cam.farClipPlane=220;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.8f,.66f,.58f);cam.allowHDR=false;cam.allowMSAA=true;
            Pose(cam,new Vector3(22,27,-23),new Vector3(0,1,5),51,2048,1400);Render(cam,Path.Combine(outdir,"overview_unlabeled.png"),2048,1400);
            var labels=new List<string>();int k=0;foreach(var value in A(layout["buildings"]))
            {var b=D(value);var q=cam.WorldToViewportPoint(Pos(b["pos"],1));labels.Add("{\"id\":\"B"+(++k)+"\",\"name\":\""+b["id"]+"\",\"x\":"+(q.x*2048).ToString("R",CultureInfo.InvariantCulture)+",\"y\":"+((1-q.y)*1400).ToString("R",CultureInfo.InvariantCulture)+"}");}
            k=0;foreach(var value in A(layout["npcs"]))
            {var n=D(value);var q=cam.WorldToViewportPoint(Pos(n["pos"],.08f));labels.Add("{\"id\":\"N"+(++k)+"\",\"name\":\""+n["name"]+"\",\"x\":"+(q.x*2048).ToString("R",CultureInfo.InvariantCulture)+",\"y\":"+((1-q.y)*1400).ToString("R",CultureInfo.InvariantCulture)+"}");}
            File.WriteAllText(Path.Combine(outdir,"overview-labels.json"),"["+string.Join(",",labels)+"]");
            Pose(cam,new Vector3(0,1.5f,0),new Vector3(0,1.5f,10),80);Render(cam,Path.Combine(outdir,"spawn_view.png"));
            foreach(string id in new[]{"smith","shop","guild"})
            {var b=A(layout["buildings"]).Select(D).First(x=>S(x["id"])==id);float depth=F(A(b["size"])[1]);Pose(cam,Local(b,0,1.5f,depth/2+.35f),Local(b,0,1.5f,-2),83);Render(cam,Path.Combine(outdir,id+"_inside.png"));}
            Pose(cam,new Vector3(2,1.5f,5),new Vector3(19,1.8f,5),80);Render(cam,Path.Combine(outdir,"east_exit.png"));Pose(cam,new Vector3(0,1.5f,0),new Vector3(0,1.5f,10),80);
            string scenePath=Dir+"/EmberVillage007.unity";EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};AssetDatabase.SaveAssets();
            File.WriteAllText(report,"{\"ok\":true,\"engine\":\"Unity "+Application.unityVersion+"\",\"pipeline\":\"Built-in\",\"triangleCount\":"+total+",\"uniqueTriangles\":"+unique+",\"routeCapsuleSamples\":"+samples+",\"clearRoomCapsuleSamples\":"+roomSamples+",\"npcPositionsAndTalkRanges\":10,\"actualFbxPhysicsVerified\":true,\"phoneFpsMeasured\":false}");Debug.Log("TOWN007_UNITY_PASS "+total+" tris, "+samples+" route samples");
        }
        catch(Exception ex){File.WriteAllText(report,"{\"ok\":false,\"error\":\""+ex.ToString().Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","").Replace("\n","\\n")+"\"}");Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
