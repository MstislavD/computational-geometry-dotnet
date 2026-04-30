using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using VoronoiDiagram.Core;
using VoronoiDiagram.Core.DataStructures;

namespace VoronoiDiagram.App
{
    /// <summary>
    /// Windows Forms test application for the Voronoi diagram algorithms.
    /// Provides interactive visualization: click to add sites, compute and display cells/edges.
    /// </summary>
    public class MainForm : Form
    {
        // Canvas panel where the Voronoi diagram is drawn
        private Panel _canvas;

        // Status bar controls
        private Label _lblStatus;
        private Label _lblSites;

        // Toolbar buttons
        private Button _btnCompute;
        private Button _btnClear;
        private Button _btnRandom;
        private Button _btnFortune;
        private CheckBox _chkShowCells;
        private CheckBox _chkShowEdges;
        private CheckBox _chkShowSites;
        private CheckBox _chkShowVertices;

        // Application state
        private long _elapsedMs;
        private readonly List<PointF> _sites = new List<PointF>();
        private BruteForceVoronoi? _voronoi;           // Brute-force result (for edge counting)
        private List<CellResult>? _cells;               // Cell polygons from either algorithm
        private bool _isComputing;

        // Color palette for cells (HSL-based, evenly distributed hues)
        private static readonly Color[] CellColors = new[]
        {
            Color.FromArgb(100, 255, 182, 193),  // Pastel pink
            Color.FromArgb(100, 173, 216, 230),  // Powder blue
            Color.FromArgb(100, 144, 238, 144),  // Light green
            Color.FromArgb(100, 255, 218, 185),  // Peach puff
            Color.FromArgb(100, 221, 160, 221),  // Plum
            Color.FromArgb(100, 255, 255, 204),  // Light yellow-green
            Color.FromArgb(100, 230, 230, 250),  // Lavender
            Color.FromArgb(100, 255, 200, 200),  // Light coral
            Color.FromArgb(100, 180, 220, 255),  // Sky blue
            Color.FromArgb(100, 200, 255, 180),  // Mint cream
        };

        public MainForm()
        {
            Text = "Voronoi Diagram — Fortune's Algorithm (Chapter 7)";
            Size = new Size(1100, 800);
            MinimumSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;

            InitializeUI();
        }

        private void InitializeUI()
        {
            // Main layout panel (vertical stack)
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(4),
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // Toolbar row
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // Canvas row
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));     // Status row

            // ---- Toolbar panel ----
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(2),
            };

            _btnCompute = CreateButton("Compute (Brute Force)", OnCompute);
            _btnFortune = CreateButton("Compute (Fortune's)", OnComputeFortune);
            _btnRandom = CreateButton("Random 20", OnRandomSites);
            var btnRandom100 = CreateButton("Random 100", OnRandom100);
            var btnRandom500 = CreateButton("Random 500", OnRandom500);
            _btnClear = CreateButton("Clear All", OnClear);

            _chkShowCells = new CheckBox { Text = "Cells", Checked = true, AutoSize = true };
            _chkShowEdges = new CheckBox { Text = "Edges", Checked = true, AutoSize = true };
            _chkShowSites = new CheckBox { Text = "Sites", Checked = true, AutoSize = true };
            _chkShowVertices = new CheckBox { Text = "Vertices", Checked = false, AutoSize = true };

            toolbar.Controls.AddRange(new Control[]
            {
                _btnCompute, _btnFortune, _btnRandom, btnRandom100, btnRandom500, _btnClear,
                new Label { Text = "  |  ", AutoSize = true },
                _chkShowCells, _chkShowEdges, _chkShowSites, _chkShowVertices
            });

            foreach (Control c in toolbar.Controls)
            {
                if (c is CheckBox cb)
                    cb.CheckedChanged += (_, _) => _canvas.Invalidate();
                else if (c is Button btn)
                    btn.Click += (_, _) => _canvas.Invalidate();
            }

            // ---- Canvas panel ----
            _canvas = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Cursor = Cursors.Cross,
            };
            _canvas.MouseClick += Canvas_MouseClick;
            _canvas.Paint += Canvas_Paint;

            // ---- Status bar ----
            var statusBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
            };

            _lblStatus = new Label { Text = "Click on canvas to add sites, then press Compute.", AutoSize = true };
            _lblSites = new Label { Text = "Sites: 0", AutoSize = true };

            // Right-align the site count
            var spacer = new Label { Text = new string(' ', 50), AutoSize = false };
            statusBar.Controls.AddRange(new Control[] { _lblStatus, spacer, _lblSites });

            // Assemble main layout
            mainPanel.Controls.Add(toolbar, 0, 0);
            mainPanel.Controls.Add(_canvas, 0, 1);
            mainPanel.Controls.Add(statusBar, 0, 2);

            Controls.Add(mainPanel);
        }

        private static Button CreateButton(string text, EventHandler onClick)
        {
            var btn = new Button { Text = text, AutoSize = true, Margin = new Padding(2, 4, 2, 4) };
            btn.Click += onClick;
            return btn;
        }

        // ---- Event Handlers ----

        private void Canvas_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _isComputing) return;

            _sites.Add(e.Location);
            UpdateStatus();
            _canvas.Invalidate();
        }

        private void OnCompute(object? sender, EventArgs e)
        {
            if (_sites.Count < 2)
            {
                SetStatus("Need at least 2 sites.");
                return;
            }

            _isComputing = true;
            SetStatus("Computing Voronoi diagram (brute-force half-plane intersection)...");
            Application.DoEvents(); // Allow UI to update

            var sw = Stopwatch.StartNew();
            try
            {
                var pts = new List<VoronoiDiagram.Core.DataStructures.Point>();
                foreach (var s in _sites)
                    pts.Add(new VoronoiDiagram.Core.DataStructures.Point(s.X, s.Y));

                // Use canvas bounds for clipping region
                double bound = Math.Max(_canvas.Width, _canvas.Height);
                _voronoi = new BruteForceVoronoi(pts);
                _voronoi.Compute(bound);
                _cells = _voronoi.Cells;
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
                _cells = null;
            }
            finally
            {
                _elapsedMs = sw.ElapsedMilliseconds;
                sw.Stop();
            }

            _isComputing = false;
            UpdateStatus();
            _canvas.Invalidate();
        }

        private void OnComputeFortune(object? sender, EventArgs e)
        {
            if (_sites.Count < 2)
            {
                SetStatus("Need at least 2 sites.");
                return;
            }

            _isComputing = true;
            SetStatus("Computing Voronoi diagram (Fortune's sweep line algorithm)...");
            Application.DoEvents();

            var sw = Stopwatch.StartNew();
            try
            {
                var pts = new List<VoronoiDiagram.Core.DataStructures.Point>();
                foreach (var s in _sites)
                    pts.Add(new VoronoiDiagram.Core.DataStructures.Point(s.X, s.Y));

                // Run Fortune's O(n log n) sweep line algorithm
                var algo = new FortuneAlgorithm();
                algo.Compute(pts);

                // Convert DCEL output to cell polygons for visualization
                _cells = algo.Diagram.ToCellResults();
                _voronoi = null; // Not using brute-force this time
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
                _cells = null;
            }
            finally
            {
                _elapsedMs = sw.ElapsedMilliseconds;
                sw.Stop();
            }

            _isComputing = false;
            UpdateStatus();
            _canvas.Invalidate();
        }

        private void OnRandomSites(object? sender, EventArgs e) => GenerateRandomSites(20);

        private void OnRandom100(object? sender, EventArgs e) => GenerateRandomSites(100);

        private void OnRandom500(object? sender, EventArgs e) => GenerateRandomSites(500);

        private void GenerateRandomSites(int count)
        {
            _sites.Clear();
            _voronoi = null;
            _cells = null;

            Random rng = new Random();
            int margin = 40;
            int w = _canvas.Width - 2 * margin;
            int h = _canvas.Height - 2 * margin;

            for (int i = 0; i < count; i++)
            {
                _sites.Add(new PointF(
                    margin + rng.Next(w),
                    margin + rng.Next(h)));
            }

            UpdateStatus();
            _canvas.Invalidate();
        }

        private void OnClear(object? sender, EventArgs e)
        {
            _sites.Clear();
            _voronoi = null;
            _cells = null;
            UpdateStatus();
            _canvas.Invalidate();
        }

        // ---- Drawing ----

        private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            // Draw grid for reference
            DrawGrid(g);

            if (_cells != null && _cells.Count > 0)
                DrawVoronoiDiagram(g);

            // Always draw sites on top
            if (_chkShowSites.Checked)
                DrawSites(g);
        }

        private void DrawGrid(Graphics g)
        {
            using var pen = new Pen(Color.FromArgb(40, 180, 180, 180), 0.5f);
            int spacing = 50;

            for (int x = spacing; x < _canvas.Width; x += spacing)
                g.DrawLine(pen, x, 0, x, _canvas.Height);

            for (int y = spacing; y < _canvas.Height; y += spacing)
                g.DrawLine(pen, 0, y, _canvas.Width, y);
        }

        private void DrawVoronoiDiagram(Graphics g)
        {
            if (_cells == null || _cells.Count == 0) return;

            // Draw cells (filled polygons with distinct colors)
            if (_chkShowCells.Checked)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    var cell = _cells[i];
                    if (cell.Vertices.Count < 3) continue;

                    var pts = ToPointFArray(cell.Vertices);
                    using var brush = new SolidBrush(CellColors[i % CellColors.Length]);
                    g.FillPolygon(brush, pts);
                }
            }

            // Draw edges (black lines between adjacent cells)
            if (_chkShowEdges.Checked)
            {
                using var pen = new Pen(Color.Black, 1.5f);

                // Edges are the boundaries of each cell polygon
                foreach (var cell in _cells)
                {
                    if (cell.Vertices.Count < 2) continue;

                    for (int i = 0; i < cell.Vertices.Count; i++)
                    {
                        var p1 = new PointF((float)cell.Vertices[i].X, (float)cell.Vertices[i].Y);
                        var p2 = new PointF((float)cell.Vertices[(i + 1) % cell.Vertices.Count].X,
                                            (float)cell.Vertices[(i + 1) % cell.Vertices.Count].Y);
                        g.DrawLine(pen, p1, p2);
                    }
                }
            }

            // Draw Voronoi vertices (intersection points of edges)
            if (_chkShowVertices.Checked)
            {
                using var brush = new SolidBrush(Color.Red);
                using var pen = new Pen(Color.White, 1f);

                // Collect unique vertices from all cell polygons
                var seen = new HashSet<string>();
                foreach (var cell in _cells)
                {
                    foreach (var v in cell.Vertices)
                    {
                        string key = $"{v.X:F1},{v.Y:F1}";
                        if (!seen.Contains(key))
                        {
                            seen.Add(key);
                            var pt = new PointF((float)v.X, (float)v.Y);
                            g.FillEllipse(brush, pt.X - 3, pt.Y - 3, 6, 6);
                            g.DrawEllipse(pen, pt.X - 3, pt.Y - 3, 6, 6);
                        }
                    }
                }
            }
        }

        private void DrawSites(Graphics g)
        {
            using var brush = new SolidBrush(Color.Blue);
            using var pen = new Pen(Color.White, 1.5f);

            foreach (var site in _sites)
            {
                // Draw a small filled circle with white border for each site
                float r = 5;
                g.FillEllipse(brush, site.X - r, site.Y - r, 2 * r, 2 * r);
                g.DrawEllipse(pen, site.X - r, site.Y - r, 2 * r, 2 * r);

                // Draw site ID label
                if (_sites.Count <= 30) // Only show labels for manageable numbers
                {
                    int idx = _sites.IndexOf(site);
                    string label = $"{idx + 1}";
                    using var font = new Font("Consolas", 8f, FontStyle.Bold);
                    SizeF sz = g.MeasureString(label, font);
                    g.DrawString(label, font, Brushes.White, site.X - sz.Width / 2, site.Y - r - sz.Height);
                }
            }
        }

        // ---- Helpers ----

        private static PointF[] ToPointFArray(List<VoronoiDiagram.Core.DataStructures.Point> vertices)
        {
            var pts = new PointF[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
                pts[i] = new PointF((float)vertices[i].X, (float)vertices[i].Y);
            return pts;
        }

        private void UpdateStatus()
        {
            _lblSites.Text = $"Sites: {_sites.Count}";

            if (_cells != null && !_isComputing)
            {
                int cellCount = _cells.Count;
                int edgeCount = _voronoi?.GetEdges().Count ?? 0; // Edge count only available from brute-force
                SetStatus($"Computed: {cellCount} cells, {edgeCount} edges. Time: {_elapsedMs} ms");
            }
            else if (!_isComputing)
                SetStatus("Click on canvas to add sites, then press Compute.");
        }

        private void SetStatus(string msg) => _lblStatus.Text = msg;
    }
}
