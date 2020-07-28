using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using System.Xml;

namespace wow_squire_map_xml_drawer
{
    public partial class Form1 : Form
    {
        #region Variables

        private List<PointF> allWaypoints = new List<PointF>();

        private List<PointF> routeWaypoints = new List<PointF>();
        private List<PointF> ghostWaypoints = new List<PointF>();
        private List<PointF> vendorWaypoints = new List<PointF>();

        private Bitmap background;
        private RectangleF boundsRectangle;

        private string inputFileName;

        private const float MapScalar = 100f;
        private const int enlarge = 40;

        #endregion


        public Form1()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            PopulateZoneCombobox();
        }


        #region Profile File

        private void OpenProfileFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(Directory.GetCurrentDirectory())
            };
            openFileDialog.ShowDialog();

            if (string.IsNullOrEmpty(openFileDialog.FileName))
                return;

            inputFileName = Path.GetFileNameWithoutExtension(openFileDialog.FileName);

            ProcessProfileFile(openFileDialog.FileName);
        }

        private void ProcessProfileFile(string inputFile)
        {
            Cleanup();

            XmlDocument doc = new XmlDocument();
            doc.Load(inputFile);

            PopulateWayPointList(doc, "Grind/Waypoints/Normal", ref routeWaypoints, true);
            PopulateWayPointList(doc, "Grind/Waypoints/Ghost", ref ghostWaypoints);
            PopulateWayPointList(doc, "Grind/Waypoints/Vendor", ref vendorWaypoints);
        }

        private void PopulateWayPointList(XmlDocument doc, string xpath, ref List<PointF> list, bool showAddon=false)
        {
            XmlNode current = doc.FirstChild.SelectSingleNode(xpath);
            if (current == null)
            {
                return;
            }

            foreach (XmlNode child in current.ChildNodes)
            {
                float x = float.Parse(child.Attributes.GetNamedItem("X").Value, CultureInfo.InvariantCulture);
                float y = float.Parse(child.Attributes.GetNamedItem("Y").Value, CultureInfo.InvariantCulture);

                if (showAddon)
                {
                    richTextBox.Text += $"/way {x} {y}\n";
                }

                list.Add(new PointF(x / MapScalar, y / MapScalar));
                allWaypoints.Add(new PointF(x / MapScalar, y / MapScalar));

                boundsRectangle = RecalculateBounds(ref allWaypoints);
            }
        }

        #endregion


        #region Drawing

        private Bitmap DownloadImageAsBitmap(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            using (WebClient client = new WebClient())
            {
                byte[] imageBytes = client.DownloadData(url);
                using (var ms = new MemoryStream(imageBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        private Rectangle Draw()
        {
            float width = (float)background.Width;
            float height = (float)background.Height;
            float radius = 3;

            using (Graphics gr = Graphics.FromImage(background))
            {
                DrawPath(gr, ghostWaypoints, width, height, radius, Brushes.Black, Brushes.Aqua);
                DrawPath(gr, vendorWaypoints, width, height, radius, Brushes.Orange, Brushes.Yellow);
                DrawPath(gr, routeWaypoints, width, height, radius, Brushes.White, Brushes.Red);

                RectangleF r = boundsRectangle;
                r.X *= width;
                r.Y *= height;
                r.Width *= width;
                r.Height *= height;

                r.Inflate(enlarge, enlarge);

                if (r.X < 0)
                    r.X = 0;
                if (r.Y < 0)
                    r.Y = 0;

                // Draw starting pos to the bottom
                Rectangle boundingbox = Rectangle.Round(r);

                // Black background
                PointF startPoint = routeWaypoints.Last();
                string startText = $"{startPoint.X * MapScalar} {startPoint.Y * MapScalar}";
                var sizeStart = gr.MeasureString(startText, Font);

                Rectangle infoRect = new Rectangle(new Point(boundingbox.X, boundingbox.Bottom), new Size(boundingbox.Width, (int)sizeStart.Height));
                gr.FillRectangle(Brushes.Black, infoRect);

                // Start: text
                Rectangle startRect = new Rectangle(new Point(infoRect.X, infoRect.Y), new Size((int)(sizeStart.Width+1), infoRect.Height));
                gr.DrawString(startText, Font, Brushes.White, startRect);

                
                // Draw if it has ghost waypoints
                Rectangle ghostRect = startRect;
                ghostRect.Offset(startRect.Width + 1, 0);
                if (ghostWaypoints.Count > 0)
                {
                    gr.DrawString("G", Font, Brushes.Aqua, ghostRect);
                }

                // Draw if it has vendor waypoints
                Rectangle vendorRect = ghostRect;
                vendorRect.Offset(10, 0);
                if (vendorWaypoints.Count > 0)
                {
                    gr.DrawString("V", Font, Brushes.Yellow, vendorRect);
                }

                Rectangle outputRect = boundingbox;
                outputRect.Height += infoRect.Height;
                return outputRect;
            }
        }

        private void DrawPath(Graphics gr, List<PointF> list, float width, float height, float radius, Brush first, Brush others)
        {
            // reverse it so first drawn last
            list.Reverse();
            list.ForEach((p) =>
            {
                gr.FillEllipse(others, width * p.X, height * p.Y, radius, radius);
            });

            if(list.Any())
            {
                var pStart = list.Last();
                gr.FillEllipse(first, width * pStart.X, height * pStart.Y, radius, radius);
            }
        }

        #endregion


        #region Util 

        private RectangleF RecalculateBounds(ref List<PointF> list)
        {
            var minX = list.Min(p => p.X);
            var minY = list.Min(p => p.Y);
            var maxX = list.Max(p => p.X);
            var maxY = list.Max(p => p.Y);

            return new RectangleF(new PointF(minX, minY), new SizeF(maxX - minX, maxY - minY));
        }

        private void SaveBitmap(Bitmap bitmap, string name)
        {
            var b = new Bitmap(bitmap);

            var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var encParams = new EncoderParameters() { Param = new[] { new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L) } };

            b.Save(name, encoder, encParams);
        }

        #endregion


        #region WinForm
        private void PopulateZoneCombobox()
        {
            comboBoxZones.Items.Clear();

            foreach (var entry in ZoneMapDict.List)
            {
                comboBoxZones.Items.Add(entry.Key);
            }
        }

        private void Cleanup()
        {
            richTextBox.Text = string.Empty;

            routeWaypoints.Clear();
            ghostWaypoints.Clear();
            vendorWaypoints.Clear();
            allWaypoints.Clear();

            if (background != null)
            {
                background.Dispose();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenProfileFile();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(comboBoxZones.SelectedItem == null || string.IsNullOrEmpty(comboBoxZones.SelectedItem.ToString()))
            {
                return;
            }

            background = DownloadImageAsBitmap(ZoneMapDict.GetUrl(comboBoxZones.SelectedItem.ToString()));
            if (background != null)
            {
                Rectangle outputRect = Draw();
                Bitmap output = background.Clone(outputRect, PixelFormat.Format32bppArgb);
                SaveBitmap(output, inputFileName + ".jpg");
            }
            else
            {
                MessageBox.Show("ImageURL is null");
            }
        }

        #endregion
    }
}
