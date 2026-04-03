#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SuperMSI
{
    public class PolygonDrawer
    {
        private PictureBox _picBox;
        private float _polyZoom = 1.0f;
        private PointF _polyPan = new PointF(0, 0);
        private bool _isDragging = false;
        private Point _dragStart;

        public List<Point3D> L2Points { get; set; } = new List<Point3D>();
        public List<Point3D> L1Points { get; set; } = new List<Point3D>();
        public List<MatchedPair> Anchors { get; set; } = new List<MatchedPair>();

        public Image MapImage { get; private set; }
        private double imgPixelSizeX, imgPixelSizeY, imgTopLeftE, imgTopLeftN, imgWidthGeo, imgHeightGeo;

        public PolygonDrawer(PictureBox picBox)
        {
            _picBox = picBox;
            _picBox.Paint += PicBox_Paint;
            _picBox.MouseWheel += PicBox_MouseWheel;
            _picBox.MouseDown += PicBox_MouseDown;
            _picBox.MouseMove += PicBox_MouseMove;
            _picBox.MouseUp += PicBox_MouseUp;
        }

        public void LoadGeoImage(string imgPath)
        {
            ClearGeoImage();
            try
            {
                MapImage = Image.FromFile(imgPath);
                string dir = Path.GetDirectoryName(imgPath), noExt = Path.GetFileNameWithoutExtension(imgPath), ext = Path.GetExtension(imgPath).ToLower();
                string worldExt = ext == ".jpg" || ext == ".jpeg" ? ".jgw" : ext == ".tif" || ext == ".tiff" ? ".tfw" : ext == ".png" ? ".pgw" : ".wld";
                string worldFile = Path.Combine(dir, noExt + worldExt);
                if (File.Exists(worldFile))
                {
                    string[] lines = File.ReadAllLines(worldFile);
                    if (lines.Length >= 6)
                    {
                        double.TryParse(lines[0], out imgPixelSizeX); double.TryParse(lines[3], out imgPixelSizeY);
                        double.TryParse(lines[4], out imgTopLeftE); double.TryParse(lines[5], out imgTopLeftN);
                        imgWidthGeo = MapImage.Width * imgPixelSizeX; imgHeightGeo = MapImage.Height * Math.Abs(imgPixelSizeY);
                    }
                }
                _picBox.Invalidate();
            }
            catch { }
        }

        public void ClearGeoImage() { if (MapImage != null) { MapImage.Dispose(); MapImage = null; } _picBox.Invalidate(); }
        public void SetData(List<Point3D> l2) { L2Points = l2; L1Points.Clear(); Anchors.Clear(); ResetView(); }
        public void SetOverlay(List<Point3D> l2, List<Point3D> l1, List<MatchedPair> anchors) { L2Points = l2; L1Points = l1; Anchors = anchors; ResetView(); }
        public void ResetView() { _polyZoom = 1.0f; _polyPan = new PointF(0, 0); _picBox.Invalidate(); }

        private void PicBox_MouseWheel(object sender, MouseEventArgs e) { _polyZoom *= e.Delta > 0 ? 1.2f : 1 / 1.2f; _picBox.Invalidate(); }
        private void PicBox_MouseDown(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _isDragging = true; _dragStart = e.Location; } }
        private void PicBox_MouseMove(object sender, MouseEventArgs e) { if (_isDragging) { _polyPan.X += e.X - _dragStart.X; _polyPan.Y += e.Y - _dragStart.Y; _dragStart = e.Location; _picBox.Invalidate(); } }
        private void PicBox_MouseUp(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) _isDragging = false; }

        private void PicBox_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            bool isOverlay = L1Points != null && L1Points.Count >= 3 && Anchors != null && Anchors.Count >= 2;
            List<Point3D> basePts = isOverlay ? L1Points : L2Points;
            if (basePts == null || basePts.Count < 3) return;

            double minE = basePts.Min(p => p.E), maxE = basePts.Max(p => p.E), minN = basePts.Min(p => p.N), maxN = basePts.Max(p => p.N);
            float scale = Math.Min(_picBox.Width / (float)(maxE - minE == 0 ? 1 : maxE - minE), _picBox.Height / (float)(maxN - minN == 0 ? 1 : maxN - minN)) * 0.8f * _polyZoom;

            PointF ToScreen(double N, double E)
            {
                return new PointF((float)((E - (minE + maxE) / 2) * scale) + (_picBox.Width / 2f + _polyPan.X), (float)((N - (minN + maxN) / 2) * -scale) + (_picBox.Height / 2f + _polyPan.Y));
            }

            // 1. [ชั้นล่างสุด] ภาพดาวเทียม
            if (MapImage != null && imgWidthGeo > 0)
            {
                PointF tl = ToScreen(imgTopLeftN, imgTopLeftE), br = ToScreen(imgTopLeftN - Math.Abs(imgHeightGeo), imgTopLeftE + imgWidthGeo);
                e.Graphics.DrawImage(MapImage, tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
            }

            if (isOverlay)
            {
                // คำนวณ Best-Fit
                int n = Anchors.Count;
                double sEL2 = Anchors.Sum(a => a.LocalPt.E), sNL2 = Anchors.Sum(a => a.LocalPt.N), sEU = Anchors.Sum(a => a.UtmPt.E), sNU = Anchors.Sum(a => a.UtmPt.N);
                double cEL2 = sEL2 / n, cNL2 = sNL2 / n, cEU = sEU / n, cNU = sNU / n;
                double sEE = 0, sNN = 0, sEN = 0, sNE = 0;
                foreach (var a in Anchors)
                {
                    double eL = a.LocalPt.E - cEL2, nL = a.LocalPt.N - cNL2, eU = a.UtmPt.E - cEU, nU = a.UtmPt.N - cNU;
                    sEE += eL * eU; sNN += nL * nU; sEN += eL * nU; sNE += nL * eU;
                }
                double rotation = Math.Atan2(sEN - sNE, sEE + sNN);

                // 2. [ชั้นกลาง] งานชั้น 2 (เส้นแดง - หนา 1.2f)
                PointF[] l2Trans = L2Points.Select(p => {
                    double tx = p.E - cEL2, ty = p.N - cNL2;
                    return ToScreen(ty * Math.Cos(rotation) + tx * Math.Sin(rotation) + cNU, tx * Math.Cos(rotation) - ty * Math.Sin(rotation) + cEU);
                }).ToArray();

                using (Pen pRed = new Pen(Color.Red, 1.2f)) // ปรับความหนาให้บางลง
                using (Font f = new Font("Tahoma", 8))
                using (Brush bDarkRed = new SolidBrush(Color.DarkRed))
                {
                    e.Graphics.DrawPolygon(pRed, l2Trans);
                    foreach (var pt in l2Trans) e.Graphics.DrawEllipse(pRed, pt.X - 2, pt.Y - 2, 4, 4);
                }

                // 3. [บนสุด] งานชั้น 1 (เส้นน้ำเงิน - หนา 0.7f คมกริบ)
                PointF[] l1Screen = L1Points.Select(p => ToScreen(p.N, p.E)).ToArray();
                using (Pen pBlue = new Pen(Color.Blue, 0.7f)) // เส้นน้ำเงินบางกว่าเพื่อความคม
                using (Brush bBlue = new SolidBrush(Color.Blue))
                using (Font f = new Font("Tahoma", 8))
                {
                    e.Graphics.DrawPolygon(pBlue, l1Screen);
                    for (int i = 0; i < L1Points.Count; i++)
                    {
                        e.Graphics.FillEllipse(bBlue, l1Screen[i].X - 2, l1Screen[i].Y - 2, 4, 4);
                        e.Graphics.DrawString(L1Points[i].Name, f, bBlue, l1Screen[i].X + 4, l1Screen[i].Y - 12);
                    }
                }
            }
            else
            {
                // โหมดปกติ (ชั้น 2 อย่างเดียว - หนา 1.0f)
                PointF[] pts = L2Points.Select(p => ToScreen(p.N, p.E)).ToArray();
                using (Pen pRed = new Pen(Color.Red, 1.0f))
                using (Brush bBlack = new SolidBrush(Color.Black))
                using (Font f = new Font("Tahoma", 8))
                {
                    e.Graphics.DrawPolygon(pRed, pts);
                    for (int i = 0; i < pts.Length; i++)
                    {
                        e.Graphics.DrawEllipse(pRed, pts[i].X - 2, pts[i].Y - 2, 4, 4);
                        e.Graphics.DrawString(L2Points[i].Name, f, bBlack, pts[i].X + 4, pts[i].Y - 12);
                    }
                }
            }
        }
    }
}