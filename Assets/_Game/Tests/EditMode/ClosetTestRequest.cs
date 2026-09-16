using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.Architecture.Tests
{
    // Runs the closet regression suite in the already-open editor.
    [InitializeOnLoad]
    public static class ClosetTestRequest
    {
        const string Request="artifacts/smooth-bear-unity/customization/run-tests.request";
        static TestRunnerApi runner;
        static ClosetTestRequest(){EditorApplication.update+=Check;}
        static void Check()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||!File.Exists(Request))return;
            File.Delete(Request);
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Results());
            runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{
                "Game.Architecture.Tests.AvatarAppearanceTests",
                "Game.Architecture.Tests.AvatarHoodSelectionTests",
                "Game.Architecture.Tests.AvatarWearableTests",
                "Game.Architecture.Tests.AvatarPreviewOrbitTests",
                "Game.Architecture.Tests.CharacterClosetPresenterTests",
                "Game.Architecture.Tests.CharacterClosetViewLobbyChromeTests"}}));
        }
        class Results:ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,"artifacts/smooth-bear-unity/customization/closet-tests.xml");
                Debug.Log($"CLOSET_TESTS passed={result.PassCount} failed={result.FailCount}");
            }
        }
    }
}
