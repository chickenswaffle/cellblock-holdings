using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cellblock.Editor
{
    // Native iOS player builds: Claude's scope, assigned by the user on 2026-10-17 for TestFlight. Run it from a
    // clone that no other editor has open, starting Unity for iOS so the project imports for that platform once:
    //   Unity -batchmode -nographics -quit -projectPath <clone> -buildTarget iOS -executeMethod Cellblock.Editor.IosBuild.Simulator -logFile <log>
    // The Xcode project lands in Builds/iOS/<Simulator|Device>; xcodebuild compiles, signs and uploads it from there.
    // CELLBLOCK_BUILD_NUMBER sets the build number and CELLBLOCK_BUNDLE_ID the bundle identifier for a TestFlight
    // upload. Player settings are restored afterwards, so a build never leaves the shared ProjectSettings changed.
    public static class IosBuild
    {
        const string ScenePath = "Assets/Cellblock/Scenes/Main.unity";

        public static void Simulator() { Build(iOSSdkVersion.SimulatorSDK, "Simulator"); }
        public static void Device() { Build(iOSSdkVersion.DeviceSDK, "Device"); }

        static void Build(iOSSdkVersion sdk, string folder)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new InvalidOperationException("Start Unity with -buildTarget iOS so the project imports for iOS.");
            ProjectSetup.Validate();
            iOSSdkVersion sdkBefore = PlayerSettings.iOS.sdkVersion;
            AppleMobileArchitectureSimulator architectureBefore = PlayerSettings.iOS.simulatorSdkArchitecture;
            string buildBefore = PlayerSettings.iOS.buildNumber, bundleBefore = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            string output = Path.Combine("Builds", "iOS", folder);
            try
            {
                PlayerSettings.iOS.sdkVersion = sdk;
                // Sticks and the dock sit at the screen edges: the first edge swipe goes to the game, not Home or
                // Notification Centre; the home bar fades while playing. Signed for the owner's team.
                PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.TopEdge | UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
                PlayerSettings.iOS.hideHomeButton = true;
                if (string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID)) { PlayerSettings.iOS.appleDeveloperTeamID = "FX4R73M22M"; PlayerSettings.iOS.appleEnableAutomaticSigning = true; }
                // The simulator runs the host's own architecture; this Mac Pro is Intel.
                PlayerSettings.iOS.simulatorSdkArchitecture = RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? AppleMobileArchitectureSimulator.ARM64 : AppleMobileArchitectureSimulator.X86_64;
                string number = Environment.GetEnvironmentVariable("CELLBLOCK_BUILD_NUMBER");
                if (!string.IsNullOrEmpty(number)) PlayerSettings.iOS.buildNumber = number;
                string bundle = Environment.GetEnvironmentVariable("CELLBLOCK_BUNDLE_ID");
                if (!string.IsNullOrEmpty(bundle)) PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundle);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.iOS, targetGroup = BuildTargetGroup.iOS, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("iOS " + folder + " build " + report.summary.result + " with " + report.summary.totalErrors + " errors.");
                Debug.Log("CELLBLOCK_IOS_BUILD_PASSED " + folder + " " + Path.GetFullPath(output));
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = sdkBefore; PlayerSettings.iOS.simulatorSdkArchitecture = architectureBefore;
                PlayerSettings.iOS.buildNumber = buildBefore; PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleBefore);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
