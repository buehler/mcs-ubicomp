using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ARETT;
using TMPro;
using UnityEngine.UI;

public class ExtendedGazeDataHandler : MonoBehaviour
{
    private bool _useExtendedAPI;
    private DateTime _lastTimeStampFromLastUpdate;

    public GazeDataSender GazeDataSender;
    public ExtendedEyeGazeDataProvider ExtendedEyeTrackingDataProvider;

    public TextMeshProUGUI DebugText;
    public ScrollRect DebugScrollRect;

    //public ExtendedEyeGazeDataProvider extendedEyeTrackingDataProvider;
    // Start is called before the first frame update
    void Start()
    {
        _useExtendedAPI = false;
        //  GazeDataSender = new GazeDataSender();
        _lastTimeStampFromLastUpdate = DateTime.UtcNow;
    }

    // Update is called once per frame
    void Update()
    {
        if (_useExtendedAPI)
        {
            Debug.Log("-- Using extended API");
            var timeNow = DateTime.UtcNow;
            Debug.Log($"Now: {timeNow}");
            Debug.Log($"_lastTimeStampFromLastUpdate: {_lastTimeStampFromLastUpdate}");
            Debug.Log($"_lastTimeStampFromLastUpdate.AddSeconds(5): {_lastTimeStampFromLastUpdate.AddSeconds(5)}");

            GetGazeDataFromExtendedAPI(timeNow);

            for (var curTimestamp = timeNow; curTimestamp <= _lastTimeStampFromLastUpdate.AddSeconds(5); curTimestamp = curTimestamp.AddMilliseconds(5))
            {
                GetGazeDataFromExtendedAPI(curTimestamp);
            }
            _lastTimeStampFromLastUpdate = timeNow;

        } 
    }

    public void StartGazeDataFromExtendedAPI()
    {
        _useExtendedAPI = true;
        GazeDataSender.CreateEmptyListForNewGazeDataChunk();
        Debug.Log("-- StartGazeDataFromExtendedAPI");
        DebugText.text += $"\n[{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}]";
        DebugText.text += "\nStarted collecting gaze data with Extended API.";
        StartCoroutine(PushToBottom());

    }

    public void StopGazeDataFromExtendedAPI()
    {
        _useExtendedAPI = false;
        Debug.Log("-- StopGazeDataFromExtendedAPI");
        DebugText.text += $"\n[{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}]";
        DebugText.text += "\nStopped collecting gaze data with Extended API.";
        StartCoroutine(PushToBottom());
    }

    IEnumerator PushToBottom()
    {
        yield return new WaitForEndOfFrame();
        DebugScrollRect.verticalNormalizedPosition = 0;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)DebugScrollRect.transform);
    }

    private void GetGazeDataFromExtendedAPI(DateTime timestamp)
    {
        Debug.Log("-- GetGazeDataFromExtendedAPI --");
        // ExtendedEyeGazeDataProvider extendedEyeTrackingDataProvider = new ExtendedEyeGazeDataProvider();
        ExtendedEyeTrackingDataProvider.enabled = true;
        

        var leftGazeReadingInWorldSpace = ExtendedEyeTrackingDataProvider.GetWorldSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Left, timestamp);
        var rightGazeReadingInWorldSpace = ExtendedEyeTrackingDataProvider.GetWorldSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Right, timestamp);
        var combinedGazeReadingInWorldSpace = ExtendedEyeTrackingDataProvider.GetWorldSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Combined, timestamp);

        var combinedGazeReadingInCameraSpace = ExtendedEyeTrackingDataProvider.GetCameraSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Combined, timestamp);

        var log = $"left pos: {leftGazeReadingInWorldSpace.EyePosition}, ";
        log += $"right pos: {rightGazeReadingInWorldSpace.EyePosition}, ";
        log += $"combined pos: {combinedGazeReadingInWorldSpace.EyePosition}, ";
        log += $"combined camera pos: {combinedGazeReadingInCameraSpace.EyePosition}, ";
        log += $"\nExtendedEyeTrackingDataProvider.isActiveAndEnabled: {ExtendedEyeTrackingDataProvider.isActiveAndEnabled}";

        var gd = new GazeData();
        var date = new DateTime(1970, 1, 1, 0, 0, 0, timestamp.Kind);
        gd.FrameTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        gd.EyeDataTimestamp = System.Convert.ToInt64((timestamp - date).TotalMilliseconds);
        Debug.Log(log);


        if (gd.GazeOrigin != null && gd.GazeDirection != null )
        {
            log += "\nIncoming gaze data is valid";
            gd.GazeOrigin = combinedGazeReadingInWorldSpace.EyePosition;
            gd.GazeDirection = combinedGazeReadingInWorldSpace.GazeDirection;

            gd.IsCalibrationValid = true;
            gd.GazeHasValue = true;

            // from ARETT (DataProvider.cs)
            // Create a gaze ray based on the gaze data
            Ray gazeRay = new Ray(gd.GazeOrigin, gd.GazeDirection);

            ////
            // The 3D gaze point is the actual position the wearer is looking at.
            // As everything apart from the eye tracking layers is visible, we have to collide the gaze with every layer except the eye tracking layers

            // Check if the gaze hits anything that isn't an AOI
            gd.GazePointHit = Physics.Raycast(gazeRay, out RaycastHit hitInfo, Mathf.Infinity);

            // If we hit something, write the hit info to the data
            if (gd.GazePointHit)
            {
                log += $"\nhit point: {hitInfo.point}";
                // Write all info from the hit to the data object
                gd.GazePoint = hitInfo.point;
            }
            else
            {
                gd.GazePoint = new Vector3(0, 0, 0);
            }

        }
        else
        {
            log += "\nIncoming gaze data is invalid";
            gd.GazeOrigin = new Vector3(0, 0, 0);
            gd.GazeDirection = new Vector3(0, 0, 0);
            gd.IsCalibrationValid = false;
            gd.GazeHasValue = false;
            gd.GazePointHit = false;
            gd.GazePoint = new Vector3(0, 0, 0);
        }

        Debug.Log(log);
        GazeDataSender.HandleNewGazeData(gd, "Data_from_Extended_ET_API");
    }
}
