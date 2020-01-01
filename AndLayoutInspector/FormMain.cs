using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace AndLayoutInspector
{
    public partial class FormMain : Form
    {
        private Snapshot _snapshot;

        const string sSnapsnotFolder = "snapshots";

        public FormMain()
        {
            InitializeComponent();
            ScanSnapshots();
        }

        private void ScanSnapshots()
        {
            cbSnapshoth.Items.Clear();
            if (Directory.Exists(sSnapsnotFolder))
                cbSnapshoth.Items.AddRange(Directory.GetDirectories(sSnapsnotFolder));
        }

        private void BtnCapture_Click(object sender, EventArgs e)
        {
            CaptureAsync();
        }

        class Dumper : IShellOutputReceiver
        {
            public List<string> Lines = new List<string>();
            public bool ParsesErrors => false;

            public void AddOutput(string line)
            {
                Lines.Add(line);
            }

            public void Flush()
            {

            }
        }

        private async Task CaptureAsync()
        {
            btnCapture.Enabled = false;
            try
            {
                var devices = AdbClient.Instance.GetDevices();
                var device = devices.FirstOrDefault();
                if (null == device)
                    return;
                var d = new Dumper();
                await AdbClient.Instance.ExecuteRemoteCommandAsync("uiautomator dump /dev/tty", device, d, CancellationToken.None, -1, Encoding.UTF8);
                var dump = d.Lines.FirstOrDefault();
                if (string.IsNullOrEmpty(dump))
                    return;
                var endToken = "</hierarchy>";
                var end = dump.LastIndexOf(endToken);
                dump = dump.Substring(0, end + endToken.Length);

                XDocument doc = XDocument.Parse(dump);

                var screen = await AdbClient.Instance.GetFrameBufferAsync(device, CancellationToken.None);

                // Save capture
                var dir = Path.Combine(sSnapsnotFolder, DateTime.Now.ToString("yyyy_MM_dd_hh_mm_ss"));
                Directory.CreateDirectory(dir);
                screen.Save(Path.Combine(dir, "screen.png"), ImageFormat.Png);
                File.WriteAllText(Path.Combine(dir, "layout.xml"), dump);

                _snapshot = new Snapshot() { Tree = doc, Image = screen };

                Display(_snapshot);
                cbSnapshoth.Items.Add(dir);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                MessageBox.Show(e.Message);

            }
            btnCapture.Enabled = true;
        }

        private void Display(Snapshot snapshot)
        {
            DisplayImage(snapshot);
            DisplayTree(snapshot.Tree);
        }

        private void DisplayImage(Snapshot snapshot)
        {
            var oldImage = imgScreen.Image;
            imgScreen.Image = null;
            oldImage?.Dispose();
            if (null == snapshot)
            {
                imgScreen.Image = null;
                return;
            }

            var bmp = new Bitmap(snapshot.Image);

            var bounds = (treeView.SelectedNode?.Tag as NodeInfo)?.bounds;
            if (bounds.HasValue)
            {
                using (var graph = Graphics.FromImage(bmp))
                {
                    graph.InterpolationMode = InterpolationMode.High;
                    graph.CompositingQuality = CompositingQuality.HighQuality;
                    graph.SmoothingMode = SmoothingMode.AntiAlias;
                    graph.DrawRectangle(new Pen(Color.Red, 3), bounds.Value);
                }
            }
            if (bmp.Height < bmp.Width)
                bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);

            imgScreen.Height = 479 * bmp.Height / bmp.Width;
            imgScreen.Image = bmp;
        }

        private Point TransformScreenPoint(Point p)
        {
            if (null != _snapshot && _snapshot.IsLandscape)
            {
                var _x = p.X;
                p.X = p.Y;
                p.Y = imgScreen.Width - _x;
            }
            var x = p.X * imgScreen.Image.Width / imgScreen.Width;
            var y = p.Y * imgScreen.Image.Height / imgScreen.Height;
            return new Point(x, y);
        }

        private void CbSnapshoth_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                XDocument doc = XDocument.Load(Path.Combine(cbSnapshoth.Text, "layout.xml"));
                var screen = Image.FromFile(Path.Combine(cbSnapshoth.Text, "screen.png"));
                _snapshot = new Snapshot() { Tree = doc, Image = screen };
                propertyGrid.SelectedObject = null;
                Display(_snapshot);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }


        private void DisplayTree(XDocument dom)
        {
            try
            {
                treeView.SelectedNode = null;
                treeView.Nodes.Clear();
                foreach (var node in dom.Root.Elements().Where(t => t.NodeType == XmlNodeType.Element))
                {
                    AddNode(treeView.Nodes, node);
                }

                treeView.ExpandAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        class NodeInfo
        {
            public NodeInfo(XElement node)
            {
                xml = node;
                bounds = xml.GetBounds();
            }
            public XElement xml;
            public Rectangle? bounds;
        }

        private void AddNode(TreeNodeCollection nodes, XElement inXmlNode)
        {
            string text = inXmlNode.Attribute("class")?.Value ?? "";

            var resourceId = inXmlNode.Attribute("resource-id")?.Value;
            if (!string.IsNullOrEmpty(resourceId))
                text += $" [{resourceId}]";

            var label = inXmlNode.Attribute("text")?.Value ?? "";
            if (!string.IsNullOrEmpty(label))
                text += $" ({label})";

            TreeNode newNode = nodes.Add(text);
            newNode.Tag = new NodeInfo(inXmlNode);
            foreach (var node in inXmlNode.Elements().Where(t => t.NodeType == XmlNodeType.Element))
            {
                AddNode(newNode.Nodes, node);
            }
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var node = e.Node.Tag as NodeInfo;
            SelectNode(node);
        }

        private void SelectNode(NodeInfo node)
        {
            if (null != node)
                propertyGrid.SelectedObject = new DictionaryPropertyGridAdapter(node.xml.Attributes().ToDictionary(km => km.Name, km => km.Value));
            else
                propertyGrid.SelectedObject = null;
            DisplayImage(_snapshot);
        }

        private void ImgScreen_MouseClick(object sender, MouseEventArgs e)
        {
            var pos = TransformScreenPoint(e.Location);
            var node = FindNode(pos, treeView.Nodes);
            treeView.SelectedNode = node;
            if (null != node)
            {
                treeView.SelectedNode.EnsureVisible();
                treeView.Focus();
            }

        }

        private TreeNode FindNode(Point pos, TreeNodeCollection nodes)
        {
            for (int i = nodes.Count - 1; i >= 0; --i)
            {
                TreeNode node = nodes[i];
                if (false == (node.Tag as NodeInfo)?.bounds?.Contains(pos))
                    continue;
                if (0 != node.Nodes.Count)
                {
                    var inNode = FindNode(pos, node.Nodes);
                    if (null != inNode)
                        return inNode;
                }
                return node;
            }
            return null;
        }
    }
}
