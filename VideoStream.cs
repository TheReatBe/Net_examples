using Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Сервер визуализации.
//      Стрим видео-потока с Unity-камеры на удаленные хосты.
//      Кадры передаются в формате JPG с заданной частотой и качеством.
//      Клиент может настраивать камеру через netCamera и управлять положением 3D модели через netComponents.
//      
// Настройка сервера:
//      1. Установить port для прослушивания.
//      2. Установить в captureCamera камеру, с которой будет идти видео-стрим.
//      3. Задать частоту кадров в frameRate. Чем выше частота, тем плавней будет идти стрим, но больше нагрузка на сеть.
//      4. Задать размер кадра width x height. Чем "больше" кадр, тем качественей будет картинка, но больше нагрузка на сеть.
//         Для маленьких дисплеев нет смысла ставить большое разрешение.
//      5. Задать качество сжатия JPG quality.
//      6. Установить netCamera, если нужно удаленное управление камерой.
//         Поддерживается: position, rotation, projectionMatrix 
//      7. Установить arContent для управления положением 3D модели.
//         Поддерживается: position, rotation, scale 
//      8. Установить netComponents, если нужно удаленное управление компонентов сцены.
//         Для связывания компонентов сервера с компонентами клиента нужно в поле ID задать одинаковое значение. 
//         Поддерживается: position, rotation, scale.
//
// Запуск сервера:
//      9. Запуск сервера enableServer = true
//      10. Останов сервера enableServer = false
//
// Настройка клиента:
//      1. Установить адрес сервера в serverIP, формат ip:port.
//      2. Установить frameViewer (RawIamge), в котором будет проигрываться видео-поток.
//      3. Добавить к frameViewer материал и установить шейдер AR > VideoStream
//
//      Для управления сервером визуализации установите настройки как для сервера пункты 3, 6-8
//
// Запуск клиента:
//      4. Запуск клиента enableClient = true
//      5. Останов клиента enableClient = false
//      6. Установить на сервере параметры калибровки: loadCalibFromPath, где filePath - путь к файлу калибровки на сервере
//      7. Загрузить 3D модель в ARContent: loadJtModel, где path - путь к JT модели на сервере
//
// СИНХРОНИЗАЦИЯ
//      synEnabled - включить синхронизацию netCamera и netComponents. По умолчанию выключено для повышения производительности.
//      ARContent синхронизируется всегда.
//
// Работа с ViveTracker:
//
//      Отслеживание камеры выполняется через Трекер, поэтому необходимо отключить синхронизацию netCamera:
//      Установить netCamera в null или установить на сервере syncEnabled = false

[RequireComponent(typeof(UnityMainThread))]
public class VideoStream : MonoBehaviour
{
    [Serializable]
    public class NetworkComponent 
    {
        public string id;
        public Transform component;
        public Matrix4x4 _cached;

        public bool checkChanges()
        {
            if (component == null) return false;

            Matrix4x4 newPose = component.localToWorldMatrix * _cached.inverse;

            _cached = component.localToWorldMatrix;

            return !newPose.isIdentity;
        }
    }

    [Serializable]
    public class VideoDataEvent : UnityEvent<string> { };

    [Serializable]
    public class VideoConnectEvent : UnityEvent<bool> { };



    [Header("Debug")]
    public bool enableLog = false;
    bool _enableLog = false;

    [Header("Input")]
    [Tooltip("Camera for capture video")]
    public Camera captureCamera;
    public float frameRate = 30;
    float _frameRate = 0;

    
    
    [Header("Output")]
    
    [Tooltip("Where to display the frame")]
    public RawImage frameViewer;
    public Color transparentColor = Color.black;
    public int width = 640;
    public int height = 480;

    [Range(1, 100)]
    [Tooltip("JPG quality to encode with, 1..100 (default 75)")]
    public int quality = 75;



    [Header("Network Components. Sync transform matrix")]

    public Transform arContent;

    public bool syncEnabled = true;
    [Tooltip("Manage remoteCamera: pose, projection matrix")]
    public Camera netCamera;
    private Matrix4x4 _cachedCameraPose;
    private Matrix4x4 _cachedCameraMatrix;

    [Tooltip("Synchonize pose of components by ID")]
    public List<NetworkComponent> netComponents = new List<NetworkComponent>();



    [Header("Server")]
    public bool enableServer = true;
    bool _enableServer = false;
    public int port = 5000;
    

    
    [Header("Client")]
    public bool enableClient = false;
    bool _enableClient = false;
    public string serverIP = "127.0.0.1";
    
    private Texture2D texture;
    private RenderTexture targetTexture;

    [Header("Server Events")]
    public VideoDataEvent calibLoadEvent;
    public VideoDataEvent jtLoadEvent;
    public VideoConnectEvent connectEvent;


    #region NETWORK
    private Networking networking;
    bool sendingVideo = false;

    const string VIDEO_HEADER = "VIDEO";
    const string NET_CAMERA_HEADER = "CAMER";
    const string NET_COMP_LIST_HEADER = "COMPS";
    const string NET_CALIB_LOAD = "CALIB";
    const string NET_JT_LOAD = "JTLDR";
    const string NET_JT_POSE = "JTPOSE";

    public bool connect()
    {
        if (enableServer)
        {
            networking.ClientDisconnect();

            string any_interface = IPAddress.Any.ToString();
            networking.StartServer(any_interface);
        }
        else if (enableClient)
        {
            networking.StopServer();
            networking.ConnectToTcpServer(serverIP);
        }

        return true;
    }

    public void disconnect()
    {
        if (_enableServer)
        {
            networking.StopServer();
            enableServer = _enableServer = false;
        }
        else if (_enableClient)
        {
            networking.ClientDisconnect();
            _enableClient = false; // Не отключаем enableClient, чтобы циклично переподключаться
        }
    }

    private void readVideoFrame(BinaryReader message)
    {
        int width = message.ReadInt32();
        int height = message.ReadInt32();
        int size = message.ReadInt32();

        byte[] textureData = message.ReadBytes(size);

        //textureData = zip(textureData, CompressionMode.Decompress);

        onGetImage(textureData, width, height);        
    }

    private void setNetCamera(BinaryReader message)
    {
        Vector3 cam_position = new Vector3(
            message.ReadSingle(),
            message.ReadSingle(),
            message.ReadSingle());

        Quaternion cam_rotation = new Quaternion(
            message.ReadSingle(),
            message.ReadSingle(),
            message.ReadSingle(),
            message.ReadSingle());

        Matrix4x4 cam_projection = Matrix4x4.zero;

        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                cam_projection[i, j] = message.ReadSingle();

        if (!syncEnabled) return;

        netCamera.transform.position = cam_position;
        netCamera.transform.rotation = cam_rotation;

        if (!(netCamera.projectionMatrix * cam_projection.inverse).isIdentity)
        {
            if(syncEnabled) netCamera.projectionMatrix = cam_projection;
        }
    }

    private void readNetCompList(BinaryReader message)
    {
        int count = message.ReadInt32();

        for(int i=0; i<count; i++)
        {
            string id = message.ReadString();

            Vector3 position = new Vector3(
                message.ReadSingle(),
                message.ReadSingle(),
                message.ReadSingle());

            Quaternion rotation = new Quaternion(
                message.ReadSingle(),
                message.ReadSingle(),
                message.ReadSingle(),
                message.ReadSingle());

            Vector3 scale = new Vector3(
                message.ReadSingle(),
                message.ReadSingle(),
                message.ReadSingle());

            if (!syncEnabled) continue;

            int index = netComponents.FindIndex(n => n.id == id);

            if(index >= 0 && netComponents[i] != null)
            {
                netComponents[i].component.position = position;
                netComponents[i].component.rotation = rotation;
                netComponents[i].component.localScale = scale;
            }
        }
    }

    private bool checkChangesNetCamera()
    {
        if (netCamera == null) return false;

        bool changed = false;
        if(_cachedCameraMatrix == null || _cachedCameraPose == null)
        {
            changed = true;
        }
        else
        {
            Matrix4x4 pose = _cachedCameraPose.inverse * netCamera.transform.localToWorldMatrix;
            _cachedCameraPose = netCamera.transform.localToWorldMatrix;

            Matrix4x4 cameraMatrix = _cachedCameraMatrix.inverse * netCamera.projectionMatrix;
            _cachedCameraMatrix = netCamera.projectionMatrix;

            if (!pose.isIdentity || !cameraMatrix.isIdentity) changed = true;
        }

        return changed;
    }    

    private void initNet()
    {
        networking = new Networking(port);

        if (networking == null)
        {
            Debug.LogError("Failed network configuration!");
        }

        networking.ReceivedRawData += onReceivedFromNetwork;
        networking.ErrorConnection += disconnect;
        networking.Connected = () => { connectEvent.Invoke(true); };
        
    }

    private byte[] zip(byte[] data, CompressionMode mode)
    {
        if (mode == CompressionMode.Compress)
        {

            using (var dataStream = new MemoryStream())
            using (var zipStream = new GZipStream(dataStream, mode))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();

                return dataStream.ToArray();
            }
        }
        else
        {
            using (var dataStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(dataStream, mode))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }
    }

    private async Task sendVideo(byte[] videoData)
    {
        if (sendingVideo) return;

        sendingVideo = true;

        //data = zip(data, CompressionMode.Compress);
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter message = new BinaryWriter(ms))
        {
            // VIDEO stream
            if (videoData != null)
            {
                message.Write(VIDEO_HEADER);
                message.Write(width);
                message.Write(height);
                message.Write(videoData.Length);
                message.Write(videoData);
                try
                {
                    await networking.SendMessage(ms.ToArray());
                }
                catch (Exception ignore) { }

                ms.Position = 0;
            }
        }

        sendingVideo = false;
    }

    // Данные должны умещаться в один пакет
    private async Task sendData()
    {
        if (sendingVideo || syncEnabled) return; // Если передается видео-кадр, то ожидаем...

        //data = zip(data, CompressionMode.Compress);
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter message = new BinaryWriter(ms))
        {            
            // Send Net Camera params            
            if (checkChangesNetCamera()) 
            {
                message.Write(NET_CAMERA_HEADER);

                Vector3 cam_position = _cachedCameraPose.GetPosition();
                Quaternion cam_rotation = _cachedCameraPose.GetRotation();
                Matrix4x4 cam_projection = _cachedCameraMatrix;

                message.Write(cam_position.x);
                message.Write(cam_position.y);
                message.Write(cam_position.z);

                message.Write(cam_rotation.x);
                message.Write(cam_rotation.y);
                message.Write(cam_rotation.z);
                message.Write(cam_rotation.w);

                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        message.Write(cam_projection[i, j]);
                    }

                networking.SendMessage(ms.ToArray());

                ms.Position = 0;
            }

            // Send poses of netComponents
            if (netComponents != null && netComponents.Count > 0)
            {
                bool dataReady = false;

                foreach (NetworkComponent comp in netComponents)
                {
                    if (comp.checkChanges())
                    {
                        //netComponents[0]._cached = Matrix4x4.identity;

                        if (!dataReady)
                        {
                            message.Write(NET_COMP_LIST_HEADER);
                            message.Write(netComponents.Count);
                            dataReady = true;
                        }

                        message.Write(comp.id);

                        Vector3 position = comp.component.position;
                        Quaternion rotation = comp.component.rotation;
                        Vector3 scale = comp.component.localScale;

                        message.Write(position.x);
                        message.Write(position.y);
                        message.Write(position.z);

                        message.Write(rotation.x);
                        message.Write(rotation.y);
                        message.Write(rotation.z);
                        message.Write(rotation.w);

                        message.Write(scale.x);
                        message.Write(scale.y);
                        message.Write(scale.z);
                    }
                }

                if (dataReady) networking.SendMessage(ms.ToArray());
            }
        }
    }

    private void onReceivedFromNetwork(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            string messageType = reader.ReadString();

            switch (messageType)
            {
                case VIDEO_HEADER:
                    readVideoFrame(reader);
                    break;

                case NET_CAMERA_HEADER:
                    if (netCamera != null) setNetCamera(reader);
                    break;

                case NET_COMP_LIST_HEADER:
                    if(netComponents.Count > 0) readNetCompList(reader);
                    break;

                case NET_CALIB_LOAD:
                    string path = reader.ReadString();
                    if (calibLoadEvent != null) calibLoadEvent.Invoke(path);
                    break;

                case NET_JT_LOAD:
                    path = reader.ReadString();
                    if (jtLoadEvent != null)
                    {
                        jtLoadEvent.Invoke(path);
                    }
                    break;

                case NET_JT_POSE:
                    readARContentPose(reader);
                    break;
            }
        }        
    }

    #endregion

    public void client(bool value)
    {
        enableClient = value;
    }

    async public void loadCalibFromPath(string path)
    {
        if (!enableClient) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter message = new BinaryWriter(ms))
        {
            message.Write(NET_CALIB_LOAD);
            message.Write(path);
            networking.SendMessage(ms.ToArray());
        }
    }

    async public void loadJtModel(string path)
    {
        if (!enableClient) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter message = new BinaryWriter(ms))
        {
            message.Write(NET_JT_LOAD);
            message.Write(path);
            networking.SendMessage(ms.ToArray());
        }
    }

    public void setARContentPose()
    {
        if (!enableClient) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter message = new BinaryWriter(ms))
        {
            Vector3 position = arContent.position;
            Quaternion rotation = arContent.rotation;
            Vector3 scale = arContent.localScale;

            message.Write(NET_JT_POSE);

            message.Write(position.x);
            message.Write(position.y);
            message.Write(position.z);

            message.Write(rotation.x);
            message.Write(rotation.y);
            message.Write(rotation.z);
            message.Write(rotation.w);

            message.Write(scale.x);
            message.Write(scale.y);
            message.Write(scale.z);

            networking.SendMessage(ms.ToArray());
        }
    }

    float frameTime = 5.0f;
    float timestamp = 0;
    private bool delay(float time_sec)
    {
        float time = Time.time;
        if (time - timestamp > time_sec)
        {
            timestamp = time;
            return true;
        }
        return false;
    }

    private void Start()
    {
        texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        targetTexture = RenderTexture.GetTemporary(texture.width, texture.height, 24);

        if (frameViewer != null)
        {
            frameViewer.texture = texture;
        }

        initNet();
    }

    private void OnDestroy()
    {
        if(networking != null) networking.Free();
    }

    private void applyParameters()
    {
        if (enableClient != _enableClient)
        {
            if (enableClient) connect();
            else disconnect();
            
            _enableClient = enableClient;
        }

        if (enableServer != _enableServer)
        {
            if (enableServer) connect();
            else disconnect();

            _enableServer = enableServer;
        }

        if(frameRate != _frameRate)
        {
            _frameRate = frameRate;
            frameTime = 1.0f / frameRate;
        }

        if(enableLog != _enableLog)
        {
            _enableLog = enableLog;
            networking.logEnabled = enableLog;
        }
    }

    private void Update()
    {
        // Синхронизация с заданной frameRate
        if (delay(frameTime)) StartCoroutine(endOfFrame());

        if (enableClient)
        {
            sendData();
        }
    }

    IEnumerator endOfFrame()
    {
        yield return new WaitForEndOfFrame();        

        applyParameters();

        // Получить изображение с камеры
        if (captureCamera != null)
        {
            texture = takeSnapshot();
        }

        // Включен стрим
        if (enableServer)
        {
            if (captureCamera != null)
            {
                byte[] data = texture.EncodeToJPG(quality);

                //byte[] data = texture.EncodeToPNG();
                //byte[] data = texture.GetRawTextureData();

                sendVideo(data);
            }
        }        

        yield return null;
    }

    private void onGetImage(byte[] textureData, int width, int height)
    {
        if (texture.width != width || texture.height != height)
        {
            texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            this.width = width;
            this.height = height;

            if (frameViewer != null) frameViewer.texture = texture;
        }

        try
        {
            //texture.LoadRawTextureData(textureData);
            texture.LoadImage(textureData);

            texture.Apply();
        }
        catch (Exception ex)
        {
            Debug.Log("Can't decode received image.\nReason:" + ex.Message);
        }
    }


    //Получение снимка с основной камеры
    private Texture2D takeSnapshot()
    {
        targetTexture.filterMode = FilterMode.Point;

        captureCamera.targetTexture = targetTexture;
        captureCamera.Render();
        RenderTexture.active = targetTexture;

        Rect rect = new Rect(0, 0, captureCamera.pixelWidth, captureCamera.pixelHeight);
        texture.ReadPixels(rect, 0, 0);
        texture.Apply();

        captureCamera.targetTexture = null;

        return texture;
    }

    private void readARContentPose(BinaryReader message)
    {
        Vector3 position = new Vector3(
                message.ReadSingle(),
                message.ReadSingle(),
                message.ReadSingle());

        Quaternion rotation = new Quaternion(
            message.ReadSingle(),
            message.ReadSingle(),
            message.ReadSingle(),
            message.ReadSingle());

        Vector3 scale = new Vector3(
            message.ReadSingle(),
            message.ReadSingle(),
            message.ReadSingle());

        if (arContent != null)
        {
            arContent.position = position;
            arContent.rotation = rotation;
            arContent.localScale = scale;
        }
    }
}
