
using UnityEngine;
using ProcessManager;
using AR;
using KioskASP_ControllersMobileAPM;

namespace ControlOperation.ARProject
{
    ///Unity Netcode??

    /// <summary>
    /// Класс для инициализации списков, содержит dataManager, processControl
    /// TODO: применять instance, должен быть единственным на сцене!
    /// </summary>
    public class ProjectManager : MonoBehaviour
    {
        [Tooltip("Если значение true - показываем UI для тестирования")]
        public bool isTestApp = false;

        [Tooltip("UnityEditorOnly")]
        public bool isActiveTestWIFI = false;

        [Tooltip("Если значение true - берем и сохраняем данные в общую локальную БД")]
        public bool isConnectToARM = false;

        [Tooltip("Если значение true - сохраняем в тестовые таблицы Oracle БД, иначе в основные таблицы Oracle БД")]
        public bool isTestOracleConnect = true;

        [Header("Тестовые данные")]
        public string appVersion = "v1";
        public string accountName;
        public string idOTO = "40-9999-23";
        public string idProgram = "000075";
        public int appCeh = 40;

        public bool isSaveDefectPhotoFiles = true;

        [Header("Объект техсборки")]
        public GameObject objectTechAssembly;

        public ControlDataManager dataManager = new();
        public ProcessControl processControl = new();

        private UITransition uITransition;
        private POIManager poiManager;
        private DetailManager detailManager;
        private PMIManager pmiManager;
        private DrawLine drawLine;
        private Config config;

        private void InitAccount()
        {
            accountName = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Trim();
        }

        private void Awake()
        {
            uITransition = FindObjectOfType<UITransition>();
            drawLine = objectTechAssembly.GetComponent<DrawLine>();
            poiManager = GetComponent<POIManager>();
            detailManager = GetComponent<DetailManager>();
            pmiManager = GetComponent<PMIManager>();
            config = FindObjectOfType<Config>();

            //обновляем настройки
            config.Load();
            dataManager.Setting = (SettingsData)config.entries.Find(e=>e.GetType() == typeof(SettingsData));

            dataManager.PlayerData = new PlayerData();

            //TODO: указывать путь Application.
            //processControl = new ProcessControl();
            var processSetting = new ProcessSettingData();
            if (isConnectToARM)
            {
                bool isConnect = processControl.InitClientProcess();
                if (isConnect)
                {
#if UNITY_EDITOR
                    Debug.Log("Connect to ARM server!");
#else
                StartCoroutine(MessageScreen.ShowBottomMessage("Connect to ARM server!", 2f));
#endif
                }
                else
#if UNITY_EDITOR
                    Debug.LogError("Error connect to ARM server!");
#else
                StartCoroutine(MessageScreen.ShowBottomMessage("Error connect to ARM server!", 2f));
#endif
                if (!isConnect) isConnectToARM = false;
            }

            OracleDBManager oracleDb = new OracleDBManager(isTestOracleConnect);
            LocalDBManager localDBManager = new LocalDBManager(processSetting.directoryARM, appVersion, isConnectToARM);

            drawLine.enabled = false;
        }

        private void Start()
        {
            uITransition.nextScreen = -1;
            
            InitAccount();
            //создание таблиц, если их нет
            dataManager.CreateTablesInLocalDb(isConnectToARM, accountName);

            //проверка обновления
            if (OracleDBManager.IsConnect)
            {
                string messageText = "";

                //TODO: увеличивать значение Application.version при сборке
                //Debug.Log(Application.version);
                
                int updateStatus = VersionDataDb.CheckAppUpdates(appVersion, out messageText);
                // 0 - необязательно, 1 - критическое.
                switch (updateStatus)
                {
                    case 0: MessageScreen.Show(messageText, answer => { if (!answer) uITransition.appExit.Invoke(); }); break;
                    case 1: MessageScreen.Show(messageText); return;
                }
            }

            dataManager.LoadDatasFromDb(isConnectToARM, accountName, idOTO, appCeh, idProgram);
            uITransition.InitializeScreen();

            uITransition.updateStateActiveDefect.AddListener(ChangeStateActiveDefect);
            uITransition.updateStateActiveDetail.AddListener(ChangeStateActiveDetail);
            uITransition.updateStateActivePMI.AddListener(ChangeStateActivePMI);

            //special from Valera)
            if (dataManager.UserData == null)
                MessageScreen.Show(accountName + ", вам не доступен функционал приложения!");
        }

        public void InitDefects()
        {
            //TODO: если списки загружались локально, то при восстановлении подключения 
            //к сети, нужно обновлять списки с БД Oracle
            dataManager.LoadOtherData();
            poiManager.InitListPOIs(dataManager.POIs);
            dataManager.SetPhotoDefectList();

            if (isSaveDefectPhotoFiles)
            {
                foreach (var defect_photo in dataManager.DefectPhotos)
                {
                    string file_path = dataManager.writeReadFotoFile.GetDefectFotoFilePath(defect_photo.dfno, defect_photo.numImage, defect_photo.idOTO);
                    bool is_save = dataManager.writeReadFotoFile.SaveFotoFile(file_path, defect_photo.image);
                }

            }
        }

        public void InitDetailsAndSketchesData()
        {
            detailManager.OnJTModelLoad();
            //формируем список деталей
            dataManager.LoadDetailsData(DetailSource.GetNamesDetails());
            //добавляем эскизы в БД и применяем isVerificate для деталей
            dataManager.UpdateSketchesData(ViewsJt.AllSketches.ToArray());

            dataManager.SetListData(TypeControlDataList.SketchData, null, ViewsJt.AllSketches);

            detailManager.InitListDetails(dataManager.Details,
                dataManager.RepresentationData.isActiveOcclision);

            dataManager.PlayerData.numDetail = -1;
        }

        public void InitActiveDetails()
        {
            dataManager.SetListData(TypeControlDataList.DetailData, detailManager.numActiveDetails);

            detailManager.InitializeActiveDetails(dataManager.Details, dataManager.PlayerData.numDetail,
                                                 dataManager.RepresentationData.isActiveOcclision);
        }

        public void UpdateListActiveDefects()
        {
            dataManager.SetListData(TypeControlDataList.DefectData);
            dataManager.SetListData(TypeControlDataList.POIData);

            bool is_near = dataManager.RepresentationData.filterDefects == (int)DefectsFilter.isShowNear;

            if (dataManager.Defects.Count != dataManager.POIs.Count)
                Debug.LogError("Не согласованные списки ApplyDefects и ApplyPOIs!");
            else poiManager.InitializeActivePOIs(is_near, dataManager.Defects, dataManager.POIs, dataManager.RepresentationData.isActiveToolTip);
        }

        public void UpdateListActiveDetails()
        {
            dataManager.SetListData(TypeControlDataList.DetailData, detailManager.numActiveDetails);

            if (dataManager.PlayerData.numDetail != -1)
                uITransition.updateStateActiveDetail?.Invoke(true, false);

            detailManager.InitializeActiveDetails(dataManager.Details,
                                                dataManager.PlayerData.numDetail, dataManager.RepresentationData.isActiveOcclision);
        }

        public void UpdateActiveDefectForRepresentation(DefectData defectData)
        {
            bool is_contains = DefectDataDb.IsStatusDefectApplyRepresentation(defectData.isRepeat,
                                                                                  defectData.status,
                                                                                  dataManager.RepresentationData);
            //если новый статус не входит в representationData
            if (!is_contains)
            {
                //костыль, поскольку внутри updateStateActiveDefect PlayerData.numDefect станет -1
                int num_defect = dataManager.PlayerData.numDefect;
                //снимаем активность с точки
                uITransition.updateStateActiveDefect?.Invoke(true, false);
                //затем полностью ее выключаем
                poiManager.SetActivePOI(num_defect, false, false);
            }
        }

        //TODO: исправить, нужно подписываться на события
        private void ChangeStateActiveDefect(bool isUpdateScreens = false, bool isActive = false)
        {
            dataManager.InitControlData(TypeControlDataList.DefectData);

            //если не входит в текущий список - отключаем его активность
            if (dataManager.DefectData == null)
                isActive = false;

            //poiManager.ShowApplyPOI ( old_num_defect , true , false ); //hack: пока без tooltip-ов

            poiManager.SetActivePOIOutline(dataManager.PlayerData.numDefect, isActive);

            if (!isActive) dataManager.PlayerData.numDefect = -1;

            if (isUpdateScreens)
            {
                string desc = isActive ? dataManager.GetDefectDescription(dataManager.DefectData) : null;
                UpdateScreens(desc);
            }
        }

        private void ChangeStateActiveDetail(bool isUpdateScreens = false, bool isActive = false)
        {
            dataManager.InitControlData(TypeControlDataList.DetailData);

            //если не входит в новый список - отключаем ее активность
            if (dataManager.DetailData == null)
                isActive = false;

            //detailMaterial.SetActiveDetailMaterial(true);
            detailManager.SetActiveDetailOutline(dataManager.PlayerData.numDetail, isActive);

            if (!isActive) dataManager.PlayerData.numDetail = -1;

            if (isUpdateScreens)
            {
                string desc = isActive ? detailManager.GetDetailDescription(dataManager.PlayerData.numDetail) : null;
                UpdateScreens(desc);
            }
        }

        private void ChangeStateActivePMI(bool isUpdateScreens = false, bool isActive = false)
        {
            pmiManager.SetActivePMI(isActive, dataManager.PlayerData.numPmi);

            if (!isActive) dataManager.PlayerData.numPmi = -1;

            if (isUpdateScreens)
            {
                string desc = isActive ? pmiManager.GetPMIAnnotation(dataManager.PlayerData.numPmi) : null;
                UpdateScreens(desc);
            }
        }

        private void UpdateScreens(string desc)
        {
            //обновляем окно описаний, если окно открыто
            if (dataManager.PlayerData.numScreen == (int)Screens.DescriptionScreen
                || uITransition.nextScreen == (int)Screens.DescriptionScreen)
                uITransition.printDescriptionElement?.Invoke(desc);

            //обновляем список деталей, если окно открыто
            if (dataManager.PlayerData.numScreen == (int)Screens.DetailsScreen
                || uITransition.nextScreen == (int)Screens.DetailsScreen)
                uITransition.initDetailsList?.Invoke();

            //обновляем список дефектов, если окно открыто
            if (dataManager.PlayerData.numScreen == (int)Screens.SearchScreen
                || uITransition.nextScreen == (int)Screens.SearchScreen)
                uITransition.initDefectList?.Invoke();

            //обновляем кнопки статусов
            uITransition.initCheckButton?.Invoke();
            //обновляем количество
            uITransition.initCountSelect?.Invoke();
        }

        public void SetRemovableDefect()
        {
            dataManager.InitControlData(TypeControlDataList.DefectData);

            if (!InspectDataDb.IsCanChangeDefectStatus(dataManager.InspectData.status))
            {
                MessageScreen.Show("Объект ОТО не находится на этапе предъявления дефектов, подтверждение устранения дефекта невозможно");
                return;
            }

            if (!DefectDataDb.IsCanChangeDefectStatus(dataManager.DefectData))
            {
                MessageScreen.Show("Невозможно подтвердить устранение. Дефект не находится на предъявлении");
                return;
            }

            if (!InspectDataDb.IsCanChangeDefectInspect(dataManager.InspectData.idOTO, dataManager.UserData.appRole))
            {
                MessageScreen.Show("Устранение дефекта в текущем статусе осмотра невозможно");
                return;
            }

            if (dataManager.UserData.appRole == 32 || dataManager.UserData.appRole == 55)
            {
                if (!InspectDataDb.IsCanAnalysActInspect(dataManager.InspectData.idOTO))
                {
                    MessageScreen.Show("Для подтверждения устранения необходимо сформировать АКТ об анализе");
                    return;
                }
                else
                {
                    if (!InspectDataDb.IsCanRepeatedInspect(dataManager.InspectData.idOTO))
                    {
                        MessageScreen.Show("Для подтверждения устранения необходимо создать повторное предъявление");
                        return;
                    }
                }
            }

            if (!DefectDataDb.IsCanChangeDefectStatus(dataManager.InspectData.idOTO, dataManager.DefectData.dfno))
            {
                MessageScreen.Show("Устранение дефекта в текущем статусе осмотра невозможно");
                return;
            }

            uITransition.onSetRemovableDefect?.Invoke();
        }

        public void SetTakeRemountDefect()
        {
            dataManager.InitControlData(TypeControlDataList.DefectData);

            if (!InspectDataDb.IsCanChangeDefectStatusTakeRemount(dataManager.InspectData.idOTO))
            {
                MessageScreen.Show("Объект ОТО не находится на этапе устранения дефектов, предъявленние к устранению невозможно");
                return;
            }

            if (!InspectDataDb.IsCanFillRemovalInspect(dataManager.InspectData.idOTO, dataManager.DefectData.dfno))
            {
                MessageScreen.Show("Для отправки к предъявлению, заполните вкладку \"Устранение\"!");
                return;
            }

            if (dataManager.DefectData.status == 2)
                uITransition.onSetTakeRemountDefect?.Invoke();
            else
                MessageScreen.Show("Внимание! Дефект не находится на этапе устранения!");
        }

        public void SetUnRemovableDefect()
        {
            dataManager.InitControlData(TypeControlDataList.DefectData);

            if (!InspectDataDb.IsCanChangeDefectStatus(dataManager.InspectData.status))
            {
                MessageScreen.Show("Объект ОТО не находится на этапе предъявления дефектов, подтверждение устранения дефекта невозможно");
                return;
            }

            uITransition.onSetUnRemovableDefect?.Invoke();
        }

        private void OnApplicationQuit()
        {           
            dataManager.SaveRepresentationData(null);

            if (isConnectToARM) dataManager.ClearCommonDatasDb();
            LocalDBManager.CloseConnection(isConnectToARM);
        }
    }
}
