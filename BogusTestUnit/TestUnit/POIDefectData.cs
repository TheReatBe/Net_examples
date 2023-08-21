using System;
using System.Collections.Generic;
using System.Text;

namespace TestUnit
{
    class POIDefectData
    {
        public POIDefectData(int key)
        {
            this.key = key;
        }

        public int key { get; set; }
        public float xposPOIAncor { get; set; }
        public float yposPOIAncor { get; set; }
        public float zposPOIAncor { get; set; }
        public float rotate_xposToolTip { get; set; }
        public float rotate_yposToolTip { get; set; }
        public float rotate_zposToolTip { get; set; }
        public float xposContent { get; set; }
        public float yposContent { get; set; }
        public float zposContent { get; set; }
        public float rotate_xposContent { get; set; }
        public float rotate_yposContent { get; set; }
        public float rotate_zposContent{ get; set; }
        public float size_xposContent{ get; set; }
        public float size_yposContent{ get; set; }
        public float size_zposContent{ get; set; }
        public POIDefectData settingDefect { get; set; }
        //public string name_material
    }
}
