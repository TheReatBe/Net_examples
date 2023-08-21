using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamProcess
{
    /// <summary>
    /// Тип текущего окна
    /// </summary>
    [Serializable]
    public enum TypeCurrentScreen
    {
        None, //по умолчанию главное окно
        ChangeDefectScreen, //окно изменения дефекта
        AddDefectScreen //окно регистрации дефекта
    }

    /// <summary>
    /// Содержит метод, выполняемый в контексте
    /// иммитируемого клиента
    /// </summary>
    public class ReadFileToStream
    {
        private string fn;
        private StreamString ss;

        public ReadFileToStream(StreamString str, string filename)
        {
            fn = filename;
            ss = str;
        }

        public void Start()
        {
            string contents = File.ReadAllText(fn);
            ss.WriteString(contents);
        }
    }
}
