using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace SpellDuel.EditorTools
{
    /// <summary>
    /// 雲端編譯入口（GameCI 的 buildMethod）：
    ///   SpellDuel.EditorTools.BuildScript.Build
    /// 不依賴在編輯器裡手動設定的任何東西：每次編譯都用程式建立場景、玩家設定、XR（ARCore/ARKit）設定。
    /// 這樣專案只需要程式碼就能重現，方便沒有 Unity 編輯器的情況下開發。
    /// </summary>
    public static class BuildScript
    {
        const string AppId = "com.paul771127.spellduel";
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";

        public static void Build()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            Configure(target);
            string scene = CreateScene();

            string path = Arg("-customBuildPath");
            if (string.IsNullOrEmpty(path))
                path = target == BuildTarget.Android ? "build/Android/SpellDuel.apk" : "build/iOS/SpellDuel";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

            Debug.Log($"[SpellDuel] Building {target} → {path}");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log($"[SpellDuel] Build result: {report.summary.result}, size {report.summary.totalSize} bytes, errors {report.summary.totalErrors}");
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // 方便在編輯器選單手動測試
        [MenuItem("SpellDuel/Configure Project")]
        public static void ConfigureMenu() { Configure(EditorUserBuildSettings.activeBuildTarget); CreateScene(); }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static string CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            // 空場景：GameRoot 會在啟動時（RuntimeInitializeOnLoadMethod）用程式建立所有物件
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return ScenePath;
        }

        static void Configure(BuildTarget target)
        {
            PlayerSettings.companyName = "SpellDuel";
            PlayerSettings.productName = "SpellDuel";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, AppId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.bundleVersion = "0.1.0";
            SetActiveInputHandlingBoth();

            // Android：ARCore 需要 API 24 以上、64 位元、OpenGL ES 3
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.forceInternetPermission = true;   // 區域網路連線
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // iOS：ARKit 需要 iOS 13 以上
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.cameraUsageDescription = "AR 對戰需要使用相機";
            PlayerSettings.iOS.microphoneUsageDescription = "唸咒語需要使用麥克風";
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1);   // ARM64

            ConfigureXR(BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader");
            ConfigureXR(BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader");
            AssetDatabase.SaveAssets();
        }

        // 同時啟用舊版 Input（Input.GetMouseButtonDown）與新版 Input System（AR Foundation 的相依套件）
        static void SetActiveInputHandlingBoth()
        {
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps == null) return;
            var so = new SerializedObject(ps);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != 2)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
            }
        }

        // 用程式建立 XR Plug-in Management 設定，並指定 ARCore / ARKit 為 AR 提供者
        static void ConfigureXR(BuildTargetGroup group, string loaderTypeName)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(XrSettingsPath));
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var settings = perTarget.SettingsForBuildTarget(group);
            settings.InitManagerOnStart = true;
            bool ok = XRPackageMetadataStore.AssignLoader(settings.Manager, loaderTypeName, group);
            Debug.Log($"[SpellDuel] XR {group}: {loaderTypeName} assigned = {ok}");
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(perTarget);
        }

        // 每次建置（雲端、export 腳本、編輯器 Build And Run 都會經過這裡）：
        // 寫入版本資訊（建置時間＋git commit），開始畫面會顯示，方便確認裝到的是不是最新版；
        // 同時更新 build number，讓手機確實當成新版本安裝。
        class StampBuild : UnityEditor.Build.IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;
            public void OnPreprocessBuild(BuildReport report)
            {
                var now = DateTime.Now;
                string sha = Git("rev-parse --short HEAD"), branch = Git("rev-parse --abbrev-ref HEAD");
                string info = $"{now:yyyy-MM-dd HH:mm}" + (string.IsNullOrEmpty(sha) ? "" : $" · {sha}") + (string.IsNullOrEmpty(branch) ? "" : $" ({branch})");
                Directory.CreateDirectory("Assets/Resources");
                File.WriteAllText("Assets/Resources/buildinfo.txt", info);
                AssetDatabase.ImportAsset("Assets/Resources/buildinfo.txt", ImportAssetOptions.ForceUpdate);
                int code = int.Parse(now.ToString("yyMMddHH"));
                PlayerSettings.Android.bundleVersionCode = code;
                PlayerSettings.iOS.buildNumber = code.ToString();
                Debug.Log($"[SpellDuel] Build info: {info}, build number {code}");
            }

            static string Git(string args)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                    {
                        RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    string o = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(5000);
                    return p.ExitCode == 0 ? o : "";
                }
                catch { return ""; }
            }
        }

#if UNITY_IOS
        // iOS：區域網路連線需要在 Info.plist 說明用途，否則連線會被系統擋下
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSLocalNetworkUsageDescription", "與同一個 Wi-Fi 的對手連線對戰");
            plist.WriteToFile(plistPath);

            // 人體偵測（Assets/Plugins/iOS/PoseBridge.mm）用到 Apple Vision
            var projPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(path);
            var proj = new UnityEditor.iOS.Xcode.PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "Vision.framework", false);
            proj.WriteToFile(projPath);
        }
#endif

#if UNITY_ANDROID
        // Android：人體偵測（Assets/Plugins/Android/PoseBridge.java）用 Google ML Kit，
        // 在 Unity 產生 Gradle 專案後把相依套件加進 unityLibrary（不必維護整份 Gradle 範本）
        class AndroidGradle : UnityEditor.Android.IPostGenerateGradleAndroidProject
        {
            public int callbackOrder => 100;
            public void OnPostGenerateGradleAndroidProject(string path)
            {
                const string dep = "implementation 'com.google.mlkit:pose-detection:18.0.0-beta3'";
                var gradle = Path.Combine(path, "build.gradle");
                var text = File.ReadAllText(gradle);
                if (!text.Contains(dep)) File.AppendAllText(gradle, "\n// SpellDuel：人體偵測\ndependencies {\n    " + dep + "\n}\n");

                var props = Path.Combine(path, "..", "gradle.properties");
                var p = File.Exists(props) ? File.ReadAllText(props) : "";
                if (!p.Contains("android.useAndroidX")) p += "\nandroid.useAndroidX=true";
                if (!p.Contains("android.enableJetifier")) p += "\nandroid.enableJetifier=true";
                File.WriteAllText(props, p + "\n");
            }
        }
#endif
    }
}
