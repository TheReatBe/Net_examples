using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Network;
using UnityEngine;
using UnityEngine.Events;

namespace AR.Data
{
    // Связывает имя файла данных json с классом хранения
    [AttributeUsage(AttributeTargets.Field)]
    class DataJson : Attribute {
        public Type dataType;
        public DataJson(Type dataType)
        {
            this.dataType = dataType;
        }
    }

    // Тип данных может иметь свой формат переносимых данных 
    public interface IDataSerializable
    {
        string serialize();
        void deserialize(string mesh_obj);
    }

    // Хранилище данных
    //
    // Чтение/запись на диск и по сети
    public class DataStorage : MonoBehaviour
    {
        // Данные 
        //      Доступ реализован по типу класса хранения
        //      Регистрация типов выполняется через DataJson
        private Dictionary<Type, object> dataBase = new Dictionary<Type, object>();

        private Dictionary<Type, UnityEvent> listeners = new Dictionary<Type, UnityEvent>();

        #region Регистрация классов хранения. Можно изменять

        [Header("Local")] 
        
        [DataJson(typeof(SettingPaint))]
        public string configJson = "setting.json";

        [DataJson(typeof(PoseModel))]
        public string poseJson = "pose.json";

        [DataJson(typeof(MeshModel))]
        public string meshObj = "mesh.obj";
        
        [DataJson(typeof(ControlPMI))]
        public string ControlPMI = "controlPMI.json";

        public string storageDir = "";

        #endregion Регистрация классов хранения

        private Dictionary<Type, FieldInfo> fileNameVars = new Dictionary<Type, FieldInfo>();

        [Header("Network")]
        public Networking networking;

        public int NetworkPort = 7777;
        
        [SerializeField]
        private bool isServer = false;

        public bool IsServer { 
            get => isServer; 
            set
            {
                if (value == isServer) return;                

                if (isStarted) disconnect();

                isServer = value;
            }
        }

        [SerializeField]
        private bool isStarted = false;

        public bool IsStarted { get=>isStarted; private set=>isStarted = value; }

        [Header("Events")]
        public UnityEvent onInitialized;
        public UnityEvent onConnected;
        public UnityEvent onDisconnected;

        public void AddListener<T>(UnityAction call)
        {
            Type key = typeof(T);
            UnityEvent unityEvent;
            if (!listeners.TryGetValue(key, out unityEvent))
            {
                unityEvent = new UnityEvent();
                listeners.Add(key, unityEvent);
            }

            unityEvent.AddListener(call);
        }

        public void RemoveListener(UnityAction call)
        {
            foreach(var events in listeners)
            {
                events.Value.RemoveListener(call);
            }
        }

        private void Notify(Type type)
        {
            UnityEvent events;
            if(listeners.TryGetValue(type, out events))
            {
                events.Invoke();
            }
        }

        // Получить данные
        public T getData<T>()
        {
            return (T) dataBase[typeof(T)];
        }

        // Добавить данные
        public bool setData(object jsonData)
        {
            return setData(jsonData, out _);
        }

        private bool setData(object jsonData, out string fileName)
        {
            fileName = "";

            Type key = jsonData.GetType();
            FieldInfo fileNameVar;
            if (!fileNameVars.TryGetValue(key, out fileNameVar))
            {
                return false;
            }

            fileName = (string) fileNameVar.GetValue(this);

            dataBase[jsonData.GetType()] = jsonData;

            return true;
        }

        private void Awake()
        {
            Init();
            networking = new Networking(NetworkPort);

            if (networking == null)
            {
                Debug.LogError("Failed network configuration!");
            }

            networking.ReceivedData += onReceivedFromNetwork;

            networking.Connected += () => onConnected.Invoke();
            networking.ErrorConnection += disconnect;

            onInitialized.Invoke();
        }

        // Timokhinvs58 - Раньше это был контруктор. Если внутри конструктора попытаться создать Unity-объекты, 
        // то unity вылетает с ошибкой. Нужно использовать Start
        public void Init()
        {
            MethodInfo loadFromFile_func = GetType().GetMethod("loadFromFile");

            // Для каждой переменной класса
            foreach (FieldInfo fieldVar in GetType().GetFields())
            {
                // Смотрим ссылку на данные json
                DataJson info = fieldVar.GetCustomAttribute<DataJson>();

                // она не должна быть нулевой
                if (info == null) continue;

                fileNameVars.Add(info.dataType, fieldVar);

                // Создаем пустой тип данных
                if (info.dataType == typeof(MeshModel))
                {
                    dataBase.Add(info.dataType, new MeshModel());
                }
                else
                {
                    dataBase.Add(info.dataType, Activator.CreateInstance(info.dataType));
                }

                try
                {
                    // Пробуем загрузить из файла
                    object value = loadFromFile_func.MakeGenericMethod(info.dataType).Invoke(this, null);
                    dataBase.Add(info.dataType, value);
                }catch (Exception) { }
            }            
        }

        #region Network
        public bool connect(string ip)
        {
            if (isServer)
            {                
                networking.ClientDisconnect();
                networking.StartServer(ip);                
            }
            else
            {                
                networking.StopServer();
                networking.ConnectToTcpServer(ip);
            }

            isStarted = true;

            return true;
        }

        public void disconnect()
        {
            if (isServer) networking.StopServer();
            else networking.ClientDisconnect();

            isStarted = false;

            onDisconnected.Invoke();
        }

        public bool sendToNetwork(object jsonData)
        {
            bool isOK = setData(jsonData);
            if (!isOK)
            {
                Debug.LogWarning("Данные типа " + jsonData.GetType().Name + " - не найдены!");
                return false;
            }

            string content;
            if (jsonData is IDataSerializable)
            {
                content = ((IDataSerializable)jsonData).serialize();
            }
            else content = JsonUtility.ToJson(jsonData);

            networking.SendMessage(content);

            return true;
        }

        private void onReceivedFromNetwork(string dataJson)
        {
            // Свой формат переносимых данных 
            Type idataSerializable = typeof(IDataSerializable);
            
            // берем элемент данных
            foreach (var item in dataBase)
            {   
                // получаем класс хранения
                Type dataType = item.Key;

                // в виде строки
                string DATA_TYPE = dataType.ToString();

                // если полученные данные содержат класс хранения
                if (dataJson.Contains(DATA_TYPE))
                {
                    object obj;

                    // и если он имеют свой формат переносимых данных
                    if (idataSerializable.IsAssignableFrom(dataType))
                    {
                        // создаем объект через рефлексию
                        obj = Activator.CreateInstance(dataType);

                        // десериализуем из dataJson
                        ((IDataSerializable)obj).deserialize(dataJson);
                    }
                    else
                    {
                        // иначе заполняем из JSON
                        obj = JsonUtility.FromJson(dataJson, dataType);
                    }

                    // заносим в хранилище
                    dataBase[dataType] = obj;

                    Notify(dataType);

                    return;
                }
            }

            Debug.LogError("Некорректные данные");
        }

        #endregion Network

        #region Local
        public T loadFromFile<T>()
        {
            Type key = typeof(T);
            FieldInfo fileNameVar;
            if (!fileNameVars.TryGetValue(key, out fileNameVar)) 
            {
                Debug.LogWarning("Данные типа " + key.Name + " - не найдены!");
                return default(T);
            }

            string dir = string.IsNullOrEmpty(storageDir) ? Application.streamingAssetsPath : storageDir;
            string fileName = dir + "/" + fileNameVar.GetValue(this);

            try
            {
                string content = File.ReadAllText(fileName);

                T data = (T)Activator.CreateInstance(key);

                if (data is IDataSerializable)
                {
                    ((IDataSerializable)data).deserialize(content);
                }
                else data = JsonUtility.FromJson<T>(content);

                dataBase[key] = data;

                return data;
            }
            catch (Exception e)
            {
                // Uncomment to see error in console
                //Debug.LogError(string.Format("Unable read {0} from file. {1}\n\n{2}", key.Name, e.Message, e.StackTrace));
            }
            return default(T);
        }

        public bool saveToFile(object jsonData)
        {
            string fileName;
            if (!setData(jsonData, out fileName))
            {
                Debug.LogWarning("Данные типа " + jsonData.GetType().Name + " - не найдены!");
                return false;
            }            

            string content;
            if(jsonData is IDataSerializable)
            {
                content = ((IDataSerializable)jsonData).serialize();
            }
            else content = JsonUtility.ToJson(jsonData, true);

            string dir = string.IsNullOrEmpty(storageDir) ? Application.streamingAssetsPath : storageDir;

            File.WriteAllText(dir + "/" + fileName, content);

            return true;
        }        

        #endregion Local
    }
}