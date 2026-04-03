#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SuperMSI
{
    public partial class Form1 : Form
    {
        DataGridView dgvLayer1, dgvLayer2, dgvFound;
        TextBox txtReport, txtCoords;
        Button btnCalc, btnExport, btnMatch;
        PictureBox picPolygon;
        Label lblAreaL2;

        PolygonDrawer drawer;
        List<MatchedPair> globalAnchors = new List<MatchedPair>();

        public Form1()
        {
            BuildUI();
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

        private void BuildUI()
        {
            this.Text = "Super MSI - Surveyor AI Edition";
            this.ClientSize = new Size(1300, 610);
            this.Font = new Font("Tahoma", 9);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            int c1X = 15, c1W = 420;
            int c2X = c1X + c1W + 15, c2W = 400;
            int c3X = c2X + c2W + 15, c3W = 410;

            Label lblL1 = new Label() { Text = "1. พิกัดชั้น 1 (UTM) [ ข้อยุติ ]:", Location = new Point(c1X, 15), AutoSize = true, Font = new Font("Tahoma", 9, FontStyle.Bold) };
            Button btnLoadL1 = new Button() { Text = "นำเข้า", Location = new Point(c1X + c1W - 150, 10), Size = new Size(80, 25), Cursor = Cursors.Hand };
            Button btnClearL1 = new Button() { Text = "ล้างค่า", Location = new Point(c1X + c1W - 65, 10), Size = new Size(65, 25), Cursor = Cursors.Hand };
            dgvLayer1 = CreateGrid(c1X, 45, c1W, 125, true, false, true);
            dgvLayer1.ReadOnly = true; dgvLayer1.DefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235); dgvLayer1.AllowUserToAddRows = false;
            btnLoadL1.Click += (s, e) => LoadFileToGrid(dgvLayer1, true, false, true);
            btnClearL1.Click += (s, e) => dgvLayer1.Rows.Clear();

            Label lblL2 = new Label() { Text = "2. พิกัดชั้น 2 (Local):", Location = new Point(c1X, 185), AutoSize = true, Font = new Font("Tahoma", 9, FontStyle.Bold) };
            lblAreaL2 = new Label() { Text = "เนื้อที่: - ไร่", Location = new Point(c1X + 135, 185), AutoSize = true, Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.DarkGreen };
            Button btnLoadL2 = new Button() { Text = "นำเข้า", Location = new Point(c1X + c1W - 150, 180), Size = new Size(80, 25), Cursor = Cursors.Hand };
            Button btnClearL2 = new Button() { Text = "ล้างค่า", Location = new Point(c1X + c1W - 65, 180), Size = new Size(65, 25), Cursor = Cursors.Hand };
            dgvLayer2 = CreateGrid(c1X, 215, c1W, 125, false, false, true);
            dgvLayer2.CellValueChanged += (s, e) => CalculateAreaL2();
            dgvLayer2.RowsRemoved += (s, e) => CalculateAreaL2();
            btnLoadL2.Click += (s, e) => LoadFileToGrid(dgvLayer2, false, false, false);
            btnClearL2.Click += (s, e) => { dgvLayer2.Rows.Clear(); CalculateAreaL2(); };

            Label lblFound = new Label() { Text = "3. หมุดที่ขุดเจอ (อัปเดต):", Location = new Point(c1X, 355), AutoSize = true, Font = new Font("Tahoma", 9, FontStyle.Bold), ForeColor = Color.Blue };
            Button btnLoadFound = new Button() { Text = "นำเข้า", Location = new Point(c1X + c1W - 150, 350), Size = new Size(80, 25), Cursor = Cursors.Hand };
            Button btnClearFound = new Button() { Text = "ล้างค่า", Location = new Point(c1X + c1W - 65, 350), Size = new Size(65, 25), Cursor = Cursors.Hand };
            dgvFound = CreateGrid(c1X, 385, c1W, 125, true, true, true);
            btnLoadFound.Click += (s, e) => LoadFileToGrid(dgvFound, true, true, true);
            btnClearFound.Click += (s, e) => dgvFound.Rows.Clear();

            int halfBtnW = (c1W - 10) / 2;
            btnMatch = new Button() { Text = "🔍 1. จับคู่หมุด", Location = new Point(c1X, 525), Size = new Size(halfBtnW, 55), Font = new Font("Tahoma", 10, FontStyle.Bold), BackColor = Color.LightSkyBlue, Cursor = Cursors.Hand };
            btnCalc = new Button() { Text = "🚀 2. ประมวลผล", Location = new Point(c1X + halfBtnW + 10, 525), Size = new Size(halfBtnW, 55), Font = new Font("Tahoma", 10, FontStyle.Bold), BackColor = Color.LightGreen, Cursor = Cursors.Hand };
            btnMatch.Click += BtnMatch_Click;
            btnCalc.Click += BtnCalc_Click;

            Label lblR = new Label() { Text = "📊 รายงานวิเคราะห์ผล (Report):", Location = new Point(c2X, 15), AutoSize = true, Font = new Font("Tahoma", 9, FontStyle.Bold) };
            txtReport = new TextBox() { Multiline = true, ScrollBars = ScrollBars.Both, Location = new Point(c2X, 35), Size = new Size(c2W, 545), ReadOnly = true, BackColor = Color.White, Font = new Font("Consolas", 12f) };

            Label lblPoly = new Label() { Text = "🗺️ รูปร่างแปลงชั้น 2 (Local):", Location = new Point(c3X, 15), AutoSize = true, Font = new Font("Tahoma", 9, FontStyle.Bold) };

            // [ใหม่] เพิ่มปุ่มนำเข้ารูปภาพ
            Button btnLoadImg = new Button() { Text = "นำเข้ารูป", Location = new Point(c3X + 220, 10), Size = new Size(80, 25), Cursor = Cursors.Hand };
            Button btnClearImg = new Button() { Text = "ล้างรูป", Location = new Point(c3X + 310, 10), Size = new Size(60, 25), Cursor = Cursors.Hand };

            picPolygon = new PictureBox() { Location = new Point(c3X, 35), Size = new Size(c3W, 280), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Cross };
            drawer = new PolygonDrawer(picPolygon);

            // ผูก Event ให้ปุ่มโหลดรูป
            btnLoadImg.Click += (s, e) => {
                using (var ofd = new OpenFileDialog() { Filter = "Georeferenced Image|*.jpg;*.jpeg;*.tif;*.tiff;*.png" })
                    if (ofd.ShowDialog() == DialogResult.OK) drawer.LoadGeoImage(ofd.FileName);
            };
            btnClearImg.Click += (s, e) => drawer.ClearGeoImage();

            Label lblC = new Label() { Text = "🎯 พิกัดเป้าหมาย (ส่งออกกล้อง):", Location = new Point(c3X, 330), AutoSize = true, Font = new Font("Tahoma", 9, FontStyle.Bold) };
            txtCoords = new TextBox() { Multiline = true, ScrollBars = ScrollBars.Both, Location = new Point(c3X, 350), Size = new Size(c3W, 165), ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 11f) };
            btnExport = new Button() { Text = "💾 ส่งออกพิกัด (.txt)", Location = new Point(c3X, 525), Size = new Size(c3W, 55), Font = new Font("Tahoma", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            btnExport.Click += (s, e) => {
                if (string.IsNullOrEmpty(txtCoords.Text)) return;
                using (var sfd = new SaveFileDialog() { Filter = "Text (*.txt)|*.txt", FileName = "MSI_Targets.txt" })
                    if (sfd.ShowDialog() == DialogResult.OK) File.WriteAllText(sfd.FileName, txtCoords.Text);
            };

            this.Controls.AddRange(new Control[] { lblL1, btnLoadL1, btnClearL1, dgvLayer1, lblL2, lblAreaL2, btnLoadL2, btnClearL2, dgvLayer2, lblFound, btnLoadFound, btnClearFound, dgvFound, btnMatch, btnCalc, lblR, txtReport, lblPoly, btnLoadImg, btnClearImg, picPolygon, lblC, txtCoords, btnExport });
        }

        private void BtnMatch_Click(object sender, EventArgs e)
        {
            var l1 = GridToList(dgvLayer1); var l2 = GridToList(dgvLayer2); var found = GridToList(dgvFound);
            var result = Engine.RunWaterfallMatch(l1, l2, found);
            globalAnchors = result.matches;
            txtReport.Text = result.report;
            ApplyStatusToGrid(dgvLayer1, l1); ApplyStatusToGrid(dgvLayer2, l2); ApplyStatusToGrid(dgvFound, found);
        }

        private async void BtnCalc_Click(object sender, EventArgs e)
        {
            if (globalAnchors.Count < 2) return;

            btnCalc.Enabled = false;
            txtReport.Text = "🚀 กำลังรุมสกัดพิกัด (Triple-Check)...\r\n";
            await Task.Delay(200);

            var layer2Full = GridToList(dgvLayer2);
            var targets = layer2Full.Where(p => !p.Status.Contains("[ล็อก]")).ToList();
            if (targets.Count == 0) { txtReport.Text = "✅ ไม่มีพิกัดเป้าหมายใหม่ให้ค้นหา\r\n"; btnCalc.Enabled = true; return; }

            var calcResult = Engine.CalculateSuperMSI(globalAnchors, targets, layer2Full);
            txtReport.Text = calcResult.report;

            txtCoords.Clear();
            foreach (var r in calcResult.results)
            {
                string numOnly = System.Text.RegularExpressions.Regex.Match(r.Name, @"\d+$").Value;
                if (string.IsNullOrEmpty(numOnly)) numOnly = r.Name;
                txtCoords.AppendText($"m-{numOnly},{r.FinalN:F3},{r.FinalE:F3},{r.FinalH:F3}\r\n");
            }

            List<Point3D> layer1Polygon = new List<Point3D>();
            foreach (var p2 in layer2Full)
            {
                var anchor = globalAnchors.FirstOrDefault(a => a.LocalPt.Name == p2.Name);
                if (anchor != null) layer1Polygon.Add(new Point3D { Name = anchor.UtmPt.Name, N = anchor.UtmPt.N, E = anchor.UtmPt.E });
                else
                {
                    var tgt = calcResult.results.FirstOrDefault(r => r.Name == p2.Name);
                    if (tgt != null)
                    {
                        string numOnly = System.Text.RegularExpressions.Regex.Match(tgt.Name, @"\d+$").Value;
                        if (string.IsNullOrEmpty(numOnly)) numOnly = tgt.Name;
                        layer1Polygon.Add(new Point3D { Name = "m-" + numOnly, N = tgt.FinalN, E = tgt.FinalE });
                    }
                }
            }

            drawer.SetOverlay(layer2Full, layer1Polygon, globalAnchors);
            btnCalc.Enabled = true;
        }

        private DataGridView CreateGrid(int x, int y, int w, int h, bool hasH, bool validateUTM, bool hasStatusCol)
        {
            var dgv = new DataGridView() { Location = new Point(x, y), Size = new Size(w, h), AllowUserToAddRows = true, RowHeadersWidth = 25, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            dgv.DefaultCellStyle.Font = new Font("Tahoma", 8.5f); dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Tahoma", 8.5f, FontStyle.Bold);
            dgv.Columns.Add(new DataGridViewTextBoxColumn() { Name = "colName", HeaderText = "ชื่อ", SortMode = DataGridViewColumnSortMode.NotSortable, Width = 80 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn() { Name = "colN", HeaderText = "พิกัด N", Width = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "F3" }, SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.Columns.Add(new DataGridViewTextBoxColumn() { Name = "colE", HeaderText = "พิกัด E", Width = 85, DefaultCellStyle = new DataGridViewCellStyle { Format = "F3" }, SortMode = DataGridViewColumnSortMode.NotSortable });
            if (hasH) dgv.Columns.Add(new DataGridViewTextBoxColumn() { Name = "colH", HeaderText = "H", Width = 50, DefaultCellStyle = new DataGridViewCellStyle { Format = "F3" }, SortMode = DataGridViewColumnSortMode.NotSortable });
            if (hasStatusCol) dgv.Columns.Add(new DataGridViewTextBoxColumn() { Name = "colStatus", HeaderText = "สถานะ", ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.DarkGray, Font = new Font("Tahoma", 8) }, SortMode = DataGridViewColumnSortMode.NotSortable });

            if (validateUTM) dgv.CellValidating += (s, e) => {
                if (!(s as DataGridView).IsCurrentCellInEditMode || string.IsNullOrWhiteSpace(e.FormattedValue.ToString())) return;
                double.TryParse(e.FormattedValue.ToString(), out double v);
                if (e.ColumnIndex == 1 && (v < 1000000 || v >= 10000000)) { MessageBox.Show("N ต้องมี 7 หลัก"); e.Cancel = true; }
                if (e.ColumnIndex == 2 && (v < 100000 || v >= 1000000)) { MessageBox.Show("E ต้องมี 6 หลัก"); e.Cancel = true; }
            };
            return dgv;
        }

        private void LoadFileToGrid(DataGridView grid, bool hasH, bool appendMode, bool validateUTM)
        {
            using (var ofd = new OpenFileDialog() { Filter = "Text files (*.txt)|*.txt" })
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (!appendMode) grid.Rows.Clear();
                    foreach (var line in File.ReadAllLines(ofd.FileName))
                    {
                        var p = line.Split(',');
                        if (p.Length >= 3 && double.TryParse(p[1], out double n) && double.TryParse(p[2], out double e))
                            grid.Rows.Add(p[0].Trim(), n, e, p.Length >= 4 ? p[3].Trim() : "0");
                    }
                    if (grid == dgvLayer2) CalculateAreaL2();
                }
        }

        private void CalculateAreaL2()
        {
            var pts = GridToList(dgvLayer2);
            if (pts.Count < 3) { lblAreaL2.Text = "เนื้อที่: - ไร่"; return; }
            double area = 0;
            for (int i = 0; i < pts.Count; i++) { int j = (i + 1) % pts.Count; area += (pts[i].E * pts[j].N) - (pts[j].E * pts[i].N); }
            double wa = Math.Abs(area) / 8.0;
            lblAreaL2.Text = $"เนื้อที่: {(int)(wa / 400)}-{(int)((wa % 400) / 100)}-{(wa % 100):F2} ไร่";
            drawer.SetData(pts);
        }

        private List<Point3D> GridToList(DataGridView grid)
        {
            var list = new List<Point3D>();
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                if (grid.Rows[i].IsNewRow) continue;
                try
                {
                    double hValue = 0;
                    if (grid.Columns.Contains("colH") && grid.Rows[i].Cells["colH"].Value != null)
                        double.TryParse(grid.Rows[i].Cells["colH"].Value.ToString(), out hValue);

                    list.Add(new Point3D
                    {
                        Name = grid.Rows[i].Cells[0].Value?.ToString(),
                        N = Convert.ToDouble(grid.Rows[i].Cells[1].Value),
                        E = Convert.ToDouble(grid.Rows[i].Cells[2].Value),
                        H = hValue,
                        Status = grid.Columns.Contains("colStatus") ? grid.Rows[i].Cells["colStatus"].Value?.ToString() ?? "" : "",
                        OriginalRowIndex = i
                    });
                }
                catch { }
            }
            return list;
        }

        private void ApplyStatusToGrid(DataGridView grid, List<Point3D> points)
        {
            Color defaultColor = grid == dgvLayer1 ? Color.FromArgb(235, 235, 235) : Color.White;
            foreach (var p in points)
            {
                grid.Rows[p.OriginalRowIndex].Cells["colStatus"].Value = p.Status;
                if (p.Status.Contains("[ล็อก]"))
                {
                    if (p.Status.Contains("L1") || p.Status.Contains("ชั้น 1")) grid.Rows[p.OriginalRowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                    else if (p.Status.Contains("เจอ")) grid.Rows[p.OriginalRowIndex].DefaultCellStyle.BackColor = Color.LightSkyBlue;
                }
                else grid.Rows[p.OriginalRowIndex].DefaultCellStyle.BackColor = defaultColor;
            }
        }
    }
}