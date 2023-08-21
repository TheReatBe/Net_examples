using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace Config
{
    //класс позволяет записывать и считывать данные из config.json
    public class Config : MonoBehaviour
    {
        // Интерфейс для формирования постоянного идентификатора. Позволяет находить 
        // в списке хранения 'entries' нужную запись.
        public interface IID
        {
            int getID();
        }

        // Интерфейс для обработки события на сохранение и загружено. Сохранение используется 
        // для обновления значений перед сохранением в файл, а загрузка - для применения загруженных данных из файла.
        public interface IIOEvents
        {
            void OnSaving();
            void OnLoaded();
        }

        [Tooltip("Имя файла конфигурации")]
        public string configFile;

        [Tooltip("Объекты хранения. Для добавления лучше открыть отдельное окно свойств у этого компонента и методом Drag&Drop переносить из стандартного окна свойств необходимые компоненты ")]
        public List<MonoBehaviour> entries;

        [SerializeField]
        [Tooltip("Загружать из файла при старте")]
        public bool autoLoad;

        [Header("События")]

        [Tooltip("Сохранение. Рекомендуется использовать для обновления значений перед сохранением")]
        public UnityEvent OnSaving;

        [Tooltip("Сохранено")]
        public UnityEvent OnSaved;

        [Tooltip("Загружено. Рекомендуется использовать для обновления элементов после загрузки параметров")]
        public UnityEvent OnLoaded;

        public void Save()
        {
            OnSaving?.Invoke();

            List<string> lines = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry is IIOEvents) ((IIOEvents)entry).OnSaving();

                int iid = entry is IID ? ((IID)entry).getID() : i;
                string id = generateID(entry, iid);

                string line = "\n" + string.Format("\"{0}\" : {1}", id, toJson(entry));
                lines.Add(line);

            }

            string json_text = "{\n" + string.Join(",", lines) + "\n}";

            File.WriteAllText(getConfigPath(), json_text);

            OnSaved?.Invoke();
        }

        public void Load()
        {
            string filePath = getConfigPath();

            if (!File.Exists(filePath)) Save();

            string raw_text = File.ReadAllText(filePath);
            Dictionary<string, string> map = parseJson(raw_text);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                int iid = entry is IID ? ((IID)entry).getID() : i;
                string id = generateID(entry, iid);

                if (map.ContainsKey(id))
                {
                    fromJson(map[id], entry);
                }
            }

            StartCoroutine(notifyListeners());
        }

        private void Start()
        {
            if (autoLoad)
            {
                Load();
            }
        }

        private string getConfigPath()
        {
            return Path.Combine(Application.streamingAssetsPath, configFile);
        }

        private string generateID(MonoBehaviour component, int index)
        {
            return component.GetType().Name + "_" + index;
        }

        private IEnumerator notifyListeners()
        {
            yield return new WaitForSeconds(0);
            OnLoaded?.Invoke();

            foreach (var entry in entries)
            {
                if (entry is IIOEvents) ((IIOEvents)entry).OnLoaded();
            }
        }

        #region SIMPLE JSON PARSER

        private string toJson(object obj, bool removeUnityObjects = true)
        {
            string json_txt = JsonUtility.ToJson(obj, true);

            if (!removeUnityObjects) return json_txt;

            // Search Text "Name":{"instanceID":#},
            string pattern = @"\s*""[^""]+""[^""]+""instanceID""[^}]+}";
            Regex reg = new Regex($"(,{pattern})|({pattern}\\s*,)");

            // And remove it from json_txt
            return reg.Replace(json_txt, "");
        }

        private void fromJson(string json_text, object obj)
        {
            JsonUtility.FromJsonOverwrite(json_text, obj);
        }

        private Dictionary<string, string> parseJson(string data)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();

            int index = 0;
            while (index < data.Length)
            {
                string key = parseString(data, ref index);
                if (key == "") break;

                string value = parseObject(data, '{', '}', ref index);

                map.Add(key, value);
            }

            return map;
        }

        private string parseString(string data, ref int index)
        {
            // Is there a variable ?
            int i = data.IndexOf('"', index);
            if (i == -1) return "";
            index = i;

            StringBuilder sb = new StringBuilder();
            while (index < data.Length)
            {
                index++;

                char ch = data[index];
                if (ch == '"')
                {
                    return sb.ToString();
                }

                // Restricted tokens
                else if ("\n:".Contains(ch)) break;

                sb.Append(ch);
            }

            throw new Exception("Failed to parse JSON string. Token \" not found!");
        }

        private string parseObject(string data, char openedToken, char closedToken, ref int index)
        {
            StringBuilder sb = new StringBuilder();

            // Is there an object ?
            int i = data.IndexOf(openedToken, index);
            if (i == -1) return "";
            index = i;

            int links = 0;

            while (index < data.Length)
            {
                char ch = data[index];
                sb.Append(ch);

                if (ch == openedToken) links++;
                else if (ch == closedToken) links--;

                if (links == 0) return sb.ToString();
                index++;
            }

            throw new Exception("Failed to parse JSON object. Token } not found!");
        }
        #endregion SIMPLE JSON PARSER
    }
}
