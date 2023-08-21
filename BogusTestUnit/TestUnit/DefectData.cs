using System;
using System.Collections.Generic;
using System.Text;

namespace TestUnit
{
    public class DefectData
    {
        public DefectData(int key)
        {
            this.key = key;
        }
        public int key { get; set; }
        public int num_inspect { get; set; }
        public int num_user { get; set; }
        public string foto1_source { get; set; }
        public string foto2_source { get; set; }
        public string foto3_source { get; set; }
        public int category { get; set; }
        public string culprit { get; set; }
        public string explic { get; set; }
        public string significant { get; set; }
        public string regular { get; set; }
        public string removable { get; set; }
        public string perfomer { get; set; }
        public string descrpipt { get; set; }
        public string date { get; set; }
        public bool defect_repeat { get; set; }
        public string status { get; set; }
        
       // public string defect_state { get; set; } //"new" or "change" or "delete"

        public static string[] Culprit = new[] { "none", "Рабочий исполнитель", "Другие" };
        public static string[] Explicit = new[] { "none", "Явный", "Скрытый" };
        public static string[] Significant = new[] { "none", "Значительный", "Незначительный" };
        public static string[] Regular = new[] { "none", "Единичный", "Повторный" };
        public static string[] Removable = new[] { "none", "Устранимый", "Неустранимый" };
        public static string[] Perfomer = new[] { "none", "Выявлен БTK", "Другие" };
        public static string[] Status = new[] { "предъявленный к устранению", "отклонённый", "принятый" };
    }
}
