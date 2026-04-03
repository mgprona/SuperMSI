#nullable disable
using System;

namespace SuperMSI
{
    public class Point3D
    {
        public string Name { get; set; }
        public double N { get; set; }
        public double E { get; set; }
        public double H { get; set; }
        public string Status { get; set; } = "";
        public int OriginalRowIndex { get; set; }
    }

    public class MatchedPair
    {
        public Point3D LocalPt { get; set; }
        public Point3D UtmPt { get; set; }
        public string Method { get; set; }
    }

    public class TargetResult
    {
        public string Name { get; set; }
        public double FinalN { get; set; }
        public double FinalE { get; set; }
        public double FinalH { get; set; }
        public double SD { get; set; }
        public int ValidSets { get; set; }

        // --- ฟิลด์ใหม่สำหรับ Report ---
        public double DigRadiusCm { get; set; }
        public string StatusWord { get; set; }
        public string Note { get; set; }
    }
}