#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SuperMSI
{
    public class Engine
    {
        // ========================================================
        // 1. ระบบจับคู่น้ำตก (Waterfall Matching System)
        // ========================================================
        public static (List<MatchedPair> matches, string report) RunWaterfallMatch(List<Point3D> layer1, List<Point3D> layer2, List<Point3D> found)
        {
            List<MatchedPair> allMatches = new List<MatchedPair>();
            int countName = 0, countProx = 0;
            string log = "--- รายงานการตรวจสอบและจับคู่หมุด (Waterfall AI) ---\r\n";

            foreach (var p2 in layer2)
            {
                var m1 = FindMatchByName(p2.Name, layer1);
                if (m1 != null)
                {
                    allMatches.Add(new MatchedPair { LocalPt = p2, UtmPt = m1, Method = "Name (L1)" });
                    p2.Status = m1.Status = "[ล็อก] ชื่อตรงชั้น 1";
                    countName++; continue;
                }
                var m3 = FindMatchByName(p2.Name, found);
                if (m3 != null && !m3.Status.Contains("[ล็อก]"))
                {
                    allMatches.Add(new MatchedPair { LocalPt = p2, UtmPt = m3, Method = "Name (Found)" });
                    p2.Status = m3.Status = "[ล็อก] ชื่อตรงหมุดเจอ";
                    countName++;
                }
            }

            if (allMatches.Count >= 2)
            {
                var aL2 = allMatches[0].LocalPt; var aUTM = allMatches[0].UtmPt;
                var bL2 = allMatches[1].LocalPt; var bUTM = allMatches[1].UtmPt;

                double dxL2 = bL2.E - aL2.E, dyL2 = bL2.N - aL2.N;
                double dxUTM = bUTM.E - aUTM.E, dyUTM = bUTM.N - aUTM.N;
                double distL2 = Math.Sqrt(dxL2 * dxL2 + dyL2 * dyL2);
                double distUTM = Math.Sqrt(dxUTM * dxUTM + dyUTM * dyUTM);

                if (distL2 > 0)
                {
                    double scale = distUTM / distL2;
                    double rotation = Math.Atan2(dxUTM, dyUTM) - Math.Atan2(dxL2, dyL2);

                    foreach (var p2 in layer2.Where(p => !p.Status.Contains("[ล็อก]")))
                    {
                        double tx = p2.E - aL2.E, ty = p2.N - aL2.N;
                        double transE = aUTM.E + ((tx * Math.Cos(rotation) + ty * Math.Sin(rotation)) * scale);
                        double transN = aUTM.N + ((-tx * Math.Sin(rotation) + ty * Math.Cos(rotation)) * scale);

                        var target = layer1.FirstOrDefault(p => !p.Status.Contains("[ล็อก]") && Math.Sqrt(Math.Pow(transE - p.E, 2) + Math.Pow(transN - p.N, 2)) < 0.15);
                        if (target != null)
                        {
                            allMatches.Add(new MatchedPair { LocalPt = p2, UtmPt = target, Method = "Proximity" });
                            p2.Status = target.Status = "[ล็อก] สวมทับ (L1)";
                            countProx++;
                        }
                    }
                }
            }

            log += $"- แมตช์ด้วยชื่อ/นามแฝง : {countName} จุด\r\n";
            log += $"- แมตช์ด้วยรัศมีสวมทับ : {countProx} จุด\r\n";
            log += $"รวมหมุดฐาน (Anchors)  : {allMatches.Count} จุด\r\n";
            if (allMatches.Count < 2) log += "\r\n⚠️ ต้องการหมุดฐานอย่างน้อย 2 จุดเพื่อรุมสกัดพิกัด!\r\n";

            return (allMatches, log);
        }

        private static Point3D FindMatchByName(string targetName, List<Point3D> sourceList)
        {
            var parts = targetName.Split('/').Select(s => s.Trim()).ToList();
            foreach (var n in parts)
            {
                string num = Regex.Match(n, @"\d+$").Value;
                string pref = n.Contains("-") ? n.Split('-')[0] : Regex.Match(n, @"^[^\d]+").Value;

                var matches = sourceList.Where(p => Regex.Match(p.Name, @"\d+$").Value == num).ToList();
                if (matches.Count == 1) return matches[0];
                var exact = matches.FirstOrDefault(p => (p.Name.Contains("-") ? p.Name.Split('-')[0] : Regex.Match(p.Name, @"^[^\d]+").Value) == pref);
                if (exact != null) return exact;
            }
            return null;
        }

        // ========================================================
        // 2. ตัวควบคุมการประมวลผล (เพิ่ม DistLaw & รายงานดุดัน)
        // ========================================================
        public static (List<TargetResult> results, string report) CalculateSuperMSI(List<MatchedPair> anchors, List<Point3D> targets, List<Point3D> layer2)
        {
            List<TargetResult> finalResults = new List<TargetResult>();
            int totalCombinations = 0;
            int totalValid = 0;
            int totalRejected = 0;

            // ----------------------------------------------
            // A. ตรวจสอบคุณภาพคู่หมุดฐาน (Anchor Check)
            // ----------------------------------------------
            string anchorReport = "[ ตรวจสอบคู่หมุดฐาน (Anchor Check) ]\r\n";
            double sumAnchorDist = 0;
            int anchorPairsCount = 0;

            for (int i = 0; i < anchors.Count; i++)
            {
                for (int j = i + 1; j < anchors.Count; j++)
                {
                    var a = anchors[i]; var b = anchors[j];
                    double distL2 = Math.Sqrt(Math.Pow(a.LocalPt.E - b.LocalPt.E, 2) + Math.Pow(a.LocalPt.N - b.LocalPt.N, 2));
                    double distUTM = Math.Sqrt(Math.Pow(a.UtmPt.E - b.UtmPt.E, 2) + Math.Pow(a.UtmPt.N - b.UtmPt.N, 2));
                    double diff = Math.Abs(distL2 - distUTM);
                    double limit = DistLaw.Calculate(distL2);

                    sumAnchorDist += distL2;
                    anchorPairsCount++;

                    string pairName = $"{a.LocalPt.Name} - {b.LocalPt.Name}";
                    if (diff <= limit)
                    {
                        anchorReport += $"✅ คู่ฐาน {pairName} : ระยะสอดคล้องตามเกณฑ์มาตรฐาน\r\n";
                    }
                    else
                    {
                        anchorReport += $"⚠️ คู่ฐาน {pairName} : ระยะเพี้ยน {diff:F3}ม. (เกินเกณฑ์ DistLaw!)\r\n";
                        anchorReport += $"   -> *ระบบยังคงนำมาใช้เป็นฐานในการตัดเส้นเพื่อหาค่าเฉลี่ย*\r\n";
                    }
                }
            }
            double globalDistLaw = anchorPairsCount > 0 ? DistLaw.Calculate(sumAnchorDist / anchorPairsCount) : 0;

            // ----------------------------------------------
            // B. ประมวลผลรุมสกัดเป้าหมาย
            // ----------------------------------------------
            foreach (var target in targets)
            {
                var res2D = Calculate2D(anchors, target);
                totalCombinations += res2D.Combinations;
                totalValid += res2D.ValidSets;
                totalRejected += (res2D.Combinations - res2D.ValidSets);

                double finalH = CalculateHeightIDW(anchors, target);

                // แปลง SD เป็นรัศมีขุด (ขั้นต่ำ 20 ซม.)
                double radiusCm = Math.Max(20.0, (res2D.SD * 2.0) * 100.0);

                // ตัดสินสถานะอิงเกณฑ์ DistLaw
                string status = "";
                if (res2D.SD <= res2D.LimitUsed) status = "🟢ดี";
                else if (res2D.SD <= res2D.LimitUsed * 2) status = "🟡พอใช้";
                else status = "🔴แย่";

                finalResults.Add(new TargetResult
                {
                    Name = target.Name,
                    FinalN = res2D.FinalN,
                    FinalE = res2D.FinalE,
                    FinalH = finalH,
                    SD = res2D.SD,
                    ValidSets = res2D.ValidSets,
                    DigRadiusCm = radiusCm,
                    StatusWord = status,
                    Note = res2D.Note
                });
            }

            finalResults = finalResults.OrderBy(r => r.SD).ToList();

            // ----------------------------------------------
            // C. วิเคราะห์รูปร่างและเนื้อที่ (Overlap Analysis)
            // ----------------------------------------------
            double areaLocalSqM = 0;
            double areaUtmSqM = 0;

            if (layer2.Count >= 3)
            {
                double aL = 0;
                for (int i = 0; i < layer2.Count; i++) { int j = (i + 1) % layer2.Count; aL += (layer2[i].E * layer2[j].N) - (layer2[j].E * layer2[i].N); }
                areaLocalSqM = Math.Abs(aL) / 2.0;

                List<Point3D> utmPts = new List<Point3D>();
                foreach (var p in layer2)
                {
                    var anc = anchors.FirstOrDefault(a => a.LocalPt.Name == p.Name);
                    if (anc != null) utmPts.Add(new Point3D { N = anc.UtmPt.N, E = anc.UtmPt.E });
                    else
                    {
                        var tgt = finalResults.FirstOrDefault(t => t.Name == p.Name);
                        if (tgt != null) utmPts.Add(new Point3D { N = tgt.FinalN, E = tgt.FinalE });
                        else utmPts.Add(p);
                    }
                }

                double aU = 0;
                for (int i = 0; i < utmPts.Count; i++) { int j = (i + 1) % utmPts.Count; aU += (utmPts[i].E * utmPts[j].N) - (utmPts[j].E * utmPts[i].N); }
                areaUtmSqM = Math.Abs(aU) / 2.0;
            }

            string FormatRaiNganWah(double sqMeters)
            {
                double sqWah = Math.Abs(sqMeters) / 4.0;
                int rai = (int)(sqWah / 400), ngan = (int)((sqWah % 400) / 100); double wah = sqWah % 100;
                return $"{rai}-{ngan}-{wah:F2} ไร่";
            }

            double areaDiff = areaUtmSqM - areaLocalSqM;
            double pctDiff = areaLocalSqM > 0 ? (Math.Abs(areaDiff) / areaLocalSqM) * 100.0 : 0;
            string signDiff = areaDiff >= 0 ? "+" : "-";

            // ----------------------------------------------
            // D. ประกอบร่างรายงาน (Report V.2)
            // ----------------------------------------------
            string report = "📊 สรุปผลรุมสกัด (Super MSI)\r\n";
            report += $"หมุดฐาน: {anchors.Count} | เป้าหมาย: {targets.Count} | เกณฑ์กฎระยะ (DistLaw): ±{globalDistLaw:F3} ม.\r\n";

            double accRate = totalCombinations > 0 ? ((double)totalValid / totalCombinations) * 100.0 : 0;
            report += $"🔥 สกัดคำนวณจาก (ระยะ-ระยะ, มุม-ระยะ, มุม-มุม) รวมทั้งสิ้น {totalCombinations} ชุด\r\n";
            report += $"✨ เข้าเกณฑ์: {totalValid} ชุด | ❌ ตัดทิ้ง: {totalRejected} ชุด | 🎯 ความแม่นยำรวม: {accRate:F1}%\r\n";
            report += new string('-', 45) + "\r\n";

            report += anchorReport + "\r\n";

            report += "[ วิเคราะห์รูปร่างและเนื้อที่ ]\r\n";
            report += $"  ▶ เนื้อที่เดิม (Local) : {FormatRaiNganWah(areaLocalSqM)}\r\n";
            report += $"  ▶ เนื้อที่ใหม่ (UTM)   : {FormatRaiNganWah(areaUtmSqM)}\r\n";
            report += $"  ▶ ผลต่างเนื้อที่       : {signDiff} {FormatRaiNganWah(Math.Abs(areaDiff))}\r\n";
            report += $"  ▶ คลาดเคลื่อนครอบซ้อน  : ± {pctDiff:F2} %\r\n";

            report += new string('-', 45) + "\r\n";
            report += "เป้าหมาย   SD(ม.)   วงรัศมีขุด    สถานะ(จำนวนชุดที่ใช้)\r\n";
            report += new string('-', 45) + "\r\n";

            foreach (var r in finalResults)
            {
                string numOnly = Regex.Match(r.Name, @"\d+$").Value;
                string mName = string.IsNullOrEmpty(numOnly) ? r.Name : "m-" + numOnly;
                string padName = mName.PadRight(8);
                string noteStr = string.IsNullOrEmpty(r.Note) ? "" : $" {r.Note}";

                report += $"{padName} | ±{r.SD:F3} | ขุด {r.DigRadiusCm,3:F0} ซม. | {r.StatusWord} ({r.ValidSets} ชุด){noteStr}\r\n";
            }
            report += new string('-', 45) + "\r\n";

            return (finalResults, report);
        }

        // ========================================================
        // 3. ฟังก์ชันย่อย: คำนวณพิกัดราบ N, E (พร้อม Soft Filter & Fallback)
        // ========================================================
        private static (double FinalN, double FinalE, double SD, int ValidSets, int Combinations, string Note, double LimitUsed)
        Calculate2D(List<MatchedPair> anchors, Point3D target)
        {
            List<Point3D> calculatedPts = new List<Point3D>();
            int combinations = 0;
            double sumDistToAnchors = 0;

            for (int i = 0; i < anchors.Count; i++)
            {
                sumDistToAnchors += Math.Sqrt(Math.Pow(target.E - anchors[i].LocalPt.E, 2) + Math.Pow(target.N - anchors[i].LocalPt.N, 2));
                for (int j = i + 1; j < anchors.Count; j++)
                {
                    var aL2 = anchors[i].LocalPt; var aUTM = anchors[i].UtmPt;
                    var bL2 = anchors[j].LocalPt; var bUTM = anchors[j].UtmPt;

                    double dxL2 = bL2.E - aL2.E, dyL2 = bL2.N - aL2.N;
                    double dxUTM = bUTM.E - aUTM.E, dyUTM = bUTM.N - aUTM.N;
                    double distL2 = Math.Sqrt(dxL2 * dxL2 + dyL2 * dyL2);
                    double distUTM = Math.Sqrt(dxUTM * dxUTM + dyUTM * dyUTM);

                    if (distL2 == 0) continue;

                    double scale = distUTM / distL2;
                    double rotation = Math.Atan2(dxUTM, dyUTM) - Math.Atan2(dxL2, dyL2);

                    double tx = target.E - aL2.E; double ty = target.N - aL2.N;
                    double calcE = aUTM.E + ((tx * Math.Cos(rotation) + ty * Math.Sin(rotation)) * scale);
                    double calcN = aUTM.N + ((-tx * Math.Sin(rotation) + ty * Math.Cos(rotation)) * scale);

                    calculatedPts.Add(new Point3D { N = calcN, E = calcE });
                    combinations++;
                }
            }

            if (calculatedPts.Count == 0) return (target.N, target.E, 0, 0, 0, "", 0.05);

            // คำนวณ DistLaw เฉพาะของเป้าหมายนี้ (อิงระยะเฉลี่ยจากหมุดฐาน)
            double avgDist = anchors.Count > 0 ? sumDistToAnchors / anchors.Count : 0;
            double distLawLimit = DistLaw.Calculate(avgDist);
            if (distLawLimit < 0.020) distLawLimit = 0.020; // ขั้นต่ำกันเหนียวที่ 2 ซม.

            double meanN = calculatedPts.Average(p => p.N);
            double meanE = calculatedPts.Average(p => p.E);

            // Step 1: เกณฑ์ปกติ (Limit = DistLaw)
            var step1 = calculatedPts.Where(p => Math.Sqrt(Math.Pow(p.E - meanE, 2) + Math.Pow(p.N - meanN, 2)) <= distLawLimit).ToList();
            if (step1.Count >= 2 || (calculatedPts.Count < 2 && step1.Count > 0))
                return ExtractStats(step1, combinations, "", distLawLimit);

            // Step 2: ขยายเกณฑ์สำรอง (Limit = DistLaw * 2)
            var step2 = calculatedPts.Where(p => Math.Sqrt(Math.Pow(p.E - meanE, 2) + Math.Pow(p.N - meanN, 2)) <= distLawLimit * 2).ToList();
            if (step2.Count >= 2)
                return ExtractStats(step2, combinations, "*ขยายเกณฑ์สำรอง*", distLawLimit);

            // Step 3: ยอมจำนน (Surrender - เอาทุกจุดมาเฉลี่ย)
            return ExtractStats(calculatedPts, combinations, "*ยอมจำนน*", distLawLimit);
        }

        // ฟังก์ชันช่วยสกัดค่าสถิติ
        private static (double, double, double, int, int, string, double) ExtractStats(List<Point3D> pts, int combs, string note, double limit)
        {
            double fn = pts.Average(p => p.N);
            double fe = pts.Average(p => p.E);
            double sumSq = pts.Sum(p => Math.Pow(p.E - fe, 2) + Math.Pow(p.N - fn, 2));
            double sd = pts.Count > 1 ? Math.Sqrt(sumSq / pts.Count) : 0;
            return (fn, fe, sd, pts.Count, combs, note, limit);
        }

        // ========================================================
        // 4. ฟังก์ชันย่อย: คำนวณระดับความสูง H (IDW)
        // ========================================================
        private static double CalculateHeightIDW(List<MatchedPair> anchors, Point3D target)
        {
            double finalH = target.H;
            if (anchors.Count == 0) return finalH;

            double sumWeight = 0, sumWeightedShift = 0;
            foreach (var anchor in anchors)
            {
                double dE = target.E - anchor.LocalPt.E, dN = target.N - anchor.LocalPt.N;
                double distance = Math.Sqrt(dE * dE + dN * dN);
                if (distance < 0.001) return target.H + (anchor.UtmPt.H - anchor.LocalPt.H);

                double weight = 1.0 / (distance * distance);
                double hShift = anchor.UtmPt.H - anchor.LocalPt.H;
                sumWeightedShift += weight * hShift;
                sumWeight += weight;
            }
            return sumWeight > 0 ? target.H + (sumWeightedShift / sumWeight) : finalH;
        }
    }
}