using System;
using System.Windows.Forms;

namespace VoronoiDiagram.App
{
    /// <summary>
    /// Entry point for the Voronoi Diagram test application.
    /// Launches the main form with interactive site placement and visualization.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
