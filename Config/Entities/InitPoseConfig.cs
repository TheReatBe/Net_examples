using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Класс хранения для параметров маркера и позиционирования по маркерам
/// </summary>
public class InitPoseConfig : ConfigData
{
    [Header("Link to scene components")]
    public MarkerDetector markerDetector;
    public InitPoseByMarker initPose;

    [Header("Data")]
    public float targetFPS = 24f;
    public DetectorEnum detector = DetectorEnum.ARUCO;

    public ArUcoDictionary dictionaryId = ArUcoDictionary.DICT_7X7_100;
    public float markerSize_meters = 0.24f;

    public float holdTime = 1f;
    public float waitLostMarker = 0.4f;

    public List<InitPoseByMarker.Data> markers = new List<InitPoseByMarker.Data>();

    private void Start()
    {
        if (markerDetector == null) FindObjectOfType<MarkerDetector>();
        if (initPose == null) FindObjectOfType<InitPoseByMarker>();
    }

    public void ApplyToScene()
    {
        markerDetector.targetFPS = targetFPS;
        markerDetector.detector = detector;
        markerDetector.dictionaryId = dictionaryId;
        markerDetector.markerSize_meters = markerSize_meters;

        initPose.holdTime = holdTime;
        initPose.waitLostMarker = waitLostMarker;

        initPose.markers = markers; // Устанавливается как ссылка

        Debug.Log("Configuration Loaded");
    }    
}