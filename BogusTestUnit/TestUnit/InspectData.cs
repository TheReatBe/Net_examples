using System;
using System.Collections.Generic;
using System.Text;

namespace TestUnit
{
    public class InspectData
    {
        public InspectData(int key)
        {
            this.key = key;
        }

        public int key { get; set; }
        public int num_user { get; set; }
        public string guild { get; set; }
        public string inspect_object { get; set; }
        public string program { get; set; }
        public string exemplar { get; set; }
        public string date_module { get; set; }
        public float work_hour { get; set; }
        public int master { get; set; }
        public string description { get; set; }
        public bool completion { get; set; }
        public string status { get; set; }
        ///<summary>
        /// "new" or "change" or "delete"
        ///</summary>
        //public string state { get; set; }
        
        public List<DefectData> defectDataList { get; set; }

        public static string[] Status = new[] { "все", "на контроле", "принято на контроль", "отправлено на доработку", "принято" };
    }
}
