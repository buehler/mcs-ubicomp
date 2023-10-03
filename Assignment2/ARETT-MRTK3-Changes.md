# Changes necessary to make ARETT work with MRTK3

1. Download ARETT: [github.com/AR-Eye-Tracking-Toolkit/ARETT](https://github.com/AR-Eye-Tracking-Toolkit/ARETT).
2. Follow the setup: [github.com/AR-Eye-Tracking-Toolkit/ARETT/wiki#setup](github.com/AR-Eye-Tracking-Toolkit/ARETT/wiki#setup).
3. Add the EyeTracking Prefab to the scene


As ARETT does not (yet) officially support MRTK3, we need to adapt a few things:

1. In /Scripts/Editor/ConfigurationEditor.cs, comment everything where it says the following (as far as I know there is no equivalent command in MRTK3 so far):
```csharp
Microsoft.MixedReality.Toolkit.Utilities.Editor.UWPCapabilityUtility.RequireCapability(...
```
2. In /Scripts/DataAccessUWP.cs in the function `CheckIfEyesApiAvailable`, do the following change. It's not quite the same but close enough. Connected to issue [#11570](https://github.com/microsoft/MixedRealityToolkit-Unity/issues/11570). 
```csharp
/* LINE 127: COMMENT this
EyesApiAvailable = WindowsApiChecker.IsPropertyAvailable(
	"Windows.UI.Input.Spatial",
	"SpatialPointerPose",
	"Eyes");
*/
// AND REPLACE it with this ↓
EyesApiAvailable = false;
if (PerceptionInterop.GetSceneCoordinateSystem(Pose.identity) is SpatialCoordinateSystem worldOrigin)
	{
	SpatialPointerPose pointerPose = SpatialPointerPose.TryGetAtTimestamp(worldOrigin, PerceptionTimestampHelper.FromHistoricalTargetTime(DateTimeOffset.Now));
		if (pointerPose != null)
		{
			EyesPose eyes = pointerPose.Eyes;
			if (eyes != null)
			{
				EyesApiAvailable =  eyes.IsCalibrationValid;
			}
		}
	}
```
3. In the same file do an additional change (line 247):
```csharp
// REPLACE these two lines:
GazeOrigin = eyes.Gaze.Value.Origin.ToUnityVector3(),
GazeDirection = eyes.Gaze.Value.Direction.ToUnityVector3()
// WITH these new lines:      
GazeOrigin = new Vector3(eyes.Gaze.Value.Origin.X, eyes.Gaze.Value.Origin.Y, eyes.Gaze.Value.Origin.Z),
GazeDirection = new Vector3(eyes.Gaze.Value.Direction.X, eyes.Gaze.Value.Direction.Y, eyes.Gaze.Value.Direction.Z)
```
4. Change every `using Microsoft.MixedReality.Toolkit.Utilities;`to `using Microsoft.MixedReality.Toolkit;`
5. That's it. Now ARETT should in principle work with MRTK3.
