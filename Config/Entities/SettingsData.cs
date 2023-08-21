using System;
using System.Text.RegularExpressions;
using UnityEngine;
using OpenCVForUnity.UnityUtils.Helper;
using ViveTrackers;
using OpenCVForUnityExample;
using ControlOperation.ARProject;
using Config;

public class SettingsData : ConfigData, Config.IIOEvents
{
    public Action ReloadSettings;

    public bool isTestApp;
    public bool isConnectToARM;
    public bool isTestConnectOracle;
    public bool isActiveTestWIFI;

    public float markerSize;
    public Vector2 cursorPose;

    public float timeLoadProgress;
    public Vector2Int cameraSize;
    public bool useOpenCV;
    public bool useLastCamToProj;
    public bool useNetTracker;
    public float optimalDistanceMarker;
    public int portUDP;
    public string calibJsonPath;
    public float scrollFactorTablet;
    public float scrollFactorPC;
    public float distanceNearModel;
    public int modelTrackerID;
    public ParametersCamera parametersCamera;
    public Trackers trackers;

    [Serializable]
    public class ParametersCamera
    {
        public string requestedDeviceName;
        public int requestedWidth;
        public int requestedHeight;
    }
    [Serializable]
    public class Trackers
    {
        public string pointerSN; //For Tracker
        public string tabletSN;
    }

    [Header("Настраиваемые объекты")]
    [SerializeField]
    private GameObject pointerTracker;

    [SerializeField]
    private GameObject tabletTracker;

    [SerializeField]
    private WebCamTextureToMatHelper webCamTextureToMatHelper;

    [SerializeField]
    private MarkerDetector markerDetector;

    [SerializeField]
    private VivePoseDetector vivePoseDetector;

    [SerializeField]
    private CameraViewer cameraViewer;

    [SerializeField]
    private ViveTrackersNetManager viveTrackersNetManager;

    [SerializeField]
    private ViveTrackersStartup viveTrackersStartup;

    [SerializeField]
    private ViveTrackersManager viveTrackersManager;

    [SerializeField]
    private CalibLoader calibLoader;

    [SerializeField]
    private PresentToPlayer presentToPlayer;

    [SerializeField]
    private FpsMonitor fpsMonitor;

    [SerializeField]
    private ProjectManager projectManager;

    public bool isNameTracker(string name)
    {
        string pattern = "^LHR-" + "[0-9A-Z]+" + "[^a-z]";
        if (name.Length == 12 && Regex.IsMatch(name, pattern))
            return true;
        else return false;
    }

    private void Awake()
    {
        if (webCamTextureToMatHelper == null) webCamTextureToMatHelper = FindObjectOfType<WebCamTextureToMatHelper>();
        if (markerDetector == null) markerDetector = FindObjectOfType<MarkerDetector>();
        if (markerDetector == null) markerDetector = FindObjectOfType<MarkerDetector>();
        if (vivePoseDetector == null) vivePoseDetector = FindObjectOfType<VivePoseDetector>();
        if (cameraViewer == null) cameraViewer = FindObjectOfType<CameraViewer>();
        if (viveTrackersNetManager == null) viveTrackersNetManager = FindObjectOfType<ViveTrackersNetManager>();
        if (calibLoader == null) calibLoader = FindObjectOfType<CalibLoader>();
        if (presentToPlayer == null) presentToPlayer = FindObjectOfType<PresentToPlayer>();
        if (viveTrackersStartup == null) viveTrackersStartup = FindObjectOfType<ViveTrackersStartup>();
        if (viveTrackersManager == null) viveTrackersManager = GameObject.Find("ViveTrackingSetup").GetComponentInChildren<ViveTrackersManager>(true);
        if (projectManager == null) projectManager = FindObjectOfType<ProjectManager>();
    }

    public void OnSaving()
    {
    }

    public void OnLoaded()
    {
        projectManager.isConnectToARM = isConnectToARM;
        projectManager.isTestApp = isTestApp;
        projectManager.isTestOracleConnect = isTestConnectOracle;

        if (!Application.isEditor)
            projectManager.isActiveTestWIFI = isActiveTestWIFI;

        pointerTracker.GetComponent<ViveTracker>().SN = this.trackers.pointerSN;
        tabletTracker.GetComponent<ViveTracker>().SN = this.trackers.tabletSN;

        webCamTextureToMatHelper.requestedWidth = parametersCamera.requestedWidth;
        webCamTextureToMatHelper.requestedHeight = parametersCamera.requestedHeight;
        webCamTextureToMatHelper.requestedDeviceName = parametersCamera.requestedDeviceName;

        markerDetector.enabled = true;
        markerDetector.markerSize_meters = markerSize;
        markerDetector.cursorPose = cursorPose;
        vivePoseDetector.marker_code = modelTrackerID;

        //UIScreen.FixListViewScrollingBug(scrollFactorPC, scrollFactorTablet);

        cameraViewer.cameraSize = cameraSize;
        cameraViewer.useOpenCV = useOpenCV;

        viveTrackersNetManager.portUDP = portUDP;

        if (useNetTracker)
        {
            viveTrackersStartup.viveTrackersManager = viveTrackersNetManager;
            viveTrackersManager.gameObject.SetActive(false);
        }
        else
        {
            viveTrackersStartup.viveTrackersManager = viveTrackersManager;
            viveTrackersManager.gameObject.SetActive(true);
        }

        // Initialize trackers
        var trackers = FindObjectsOfType<ViveTracker>();
        foreach (var tracker in trackers)
        {
            ViveTrackersManagerBase activeMan = viveTrackersNetManager;
            if (!useNetTracker) activeMan = viveTrackersManager;

            tracker.trackersManager = activeMan;
            tracker.reconnect();
        }

        calibLoader.LoadCalibFromFile(calibJsonPath);
        calibLoader.useLastCamToProj = useLastCamToProj;

        markerDetector.markerSize_meters = markerSize;
        vivePoseDetector.marker_code = modelTrackerID;

        if (presentToPlayer) presentToPlayer.presentationDistance = distanceNearModel;

        ReloadSettings?.Invoke();
    }
}