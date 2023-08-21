using System;
using System.Collections.Generic;
using System.Text;

namespace TestUnit
{
    public class UserData
    {
        public UserData(int key)
        {
            this.key = key;
        }
        public int key { get; set; }
        public string login { get; set; }
        public string password { get; set; }
        public string FIO { get; set; }
        public string tab_num { get; set; }
        public string guild { get; set; }
        public string position { get; set; }
        public string rule { get; set; }
        public int num_login { get; set; } //hack: скорее всего придется переместить
        //public List<InspectData> inspects { get; set; }
        public string source { get; set; }
    }
}
