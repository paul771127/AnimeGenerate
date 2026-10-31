#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static class UiPolishBuild
    {
        public static void Build()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            // Present but disabled: Boot finds it, and its Start never runs. No AR/network/save startup.
            var root=new GameObject("Disabled production bootstrap").AddComponent<GameRoot>();root.enabled=false;
            new GameObject("Native IMGUI preview").AddComponent<UiPolishPreview>();
            Directory.CreateDirectory("Assets/UiPolishFixture");
            const string path="Assets/UiPolishFixture/Preview.unity";EditorSceneManager.SaveScene(scene,path);
            PlayerSettings.defaultScreenWidth=1080;PlayerSettings.defaultScreenHeight=2340;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},locationPathName="build/ui-preview/preview.x86_64",target=BuildTarget.StandaloneLinux64,extraScriptingDefines=new[]{"UI_POLISH_PREVIEW"},options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Native UI fixture build failed");
        }
    }
}
#endif
