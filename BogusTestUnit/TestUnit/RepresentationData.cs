using System;
using System.Collections.Generic;
using System.Text;

namespace TestUnit
{
    class RepresentationData
    {
        public RepresentationData(int num_accaunt, int num_module)
        {
            this.num_accaunt = num_accaunt;
            this.num_inspect = num_module;
        }

        public int num_accaunt { get; set; }
        public int num_inspect { get; set; }
        public bool represent_all { get; set; }
        public bool represent_solved { get; set; }
        public bool represent_taken_solved { get; set; }
        public bool represent_unresolved { get; set; }
        public bool represent_near { get; set; }
        public bool represent_hide_info { get; set; }
        //TODO: Добавить вид отображения
    }
}
