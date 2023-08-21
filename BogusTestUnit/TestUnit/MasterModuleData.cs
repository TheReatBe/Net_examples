using System;
using System.Collections.Generic;
using System.Text;

namespace TestUnit
{
    class MasterModuleData
    {
        public MasterModuleData(int num_master)
        {
            this.num_master = num_master;
        }
        public int num_master { get; set; }
        public string FIO { get; set; }
        public string source { get; set; }
    }
}
