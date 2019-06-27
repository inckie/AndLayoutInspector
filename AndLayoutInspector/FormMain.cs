using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace AndLayoutInspector
{
    public partial class FormMain : Form
    {
        private Snapshot _snapshot;

        const string sSnapsnotFolder = "snapshots";

        public FormMain()
        {
            InitializeComponent();
            if (Directory.Exists(sSnapsnotFolder))
            {
                cbSnapshoth.Items.AddRange(Directory.GetDirectories(sSnapsnotFolder));
            }
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
            try
            {
                var devices = AdbClient.Instance.GetDevices();
                var device = devices.FirstOrDefault();
                if (null == device)
                    return;
                var d = new Dumper();
                await AdbClient.Instance.ExecuteRemoteCommandAsync("uiautomator dump /dev/tty", device, d, CancellationToken.None, -1);
                var dump = d.Lines.FirstOrDefault();
                if (string.IsNullOrEmpty(dump))
                    return;
                var endToken = "</hierarchy>";
                var end = dump.LastIndexOf(endToken);
                dump = dump.Substring(0, end + endToken.Length);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(dump);

                var screen = await AdbClient.Instance.GetFrameBufferAsync(device, CancellationToken.None);
                if (screen.Height < screen.Width)
                    screen.RotateFlip(RotateFlipType.Rotate90FlipNone);

                // Save capture
                var dir = Path.Combine(sSnapsnotFolder, DateTime.Now.ToString("yyyy_M_dd_hh_mm_ss"));
                Directory.CreateDirectory(dir);
                screen.Save(Path.Combine(dir, "screen.png"), ImageFormat.Png);
                File.WriteAllText(Path.Combine(dir, "layout.xml"), dump);

                _snapshot = new Snapshot() { Tree = doc, Image = screen };

                Display(_snapshot);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                MessageBox.Show(e.Message);

            }
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
                XmlDocument doc = new XmlDocument();
                doc.Load(Path.Combine(cbSnapshoth.Text, "layout.xml"));
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


        private void DisplayTree(XmlDocument dom)
        {
            try
            {
                treeView.SelectedNode = null;
                treeView.Nodes.Clear();
                foreach (XmlNode node in dom.DocumentElement.ChildNodes)
                {
                    if (node.Name == "namespace" && node.ChildNodes.Count == 0)
                        continue;
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
            public NodeInfo(XmlNode node)
            {
                xml = node;
                bounds = xml.GetBounds();
            }
            public XmlNode xml;
            public Rectangle? bounds;
        }

        private void AddNode(TreeNodeCollection nodes, XmlNode inXmlNode)
        {
            string text = $"{inXmlNode.Attributes.GetNamedItem("class").Value} [{inXmlNode.Attributes.GetNamedItem("resource-id").Value}]";
            var label = inXmlNode.Attributes.GetNamedItem("text")?.Value ?? "";
            if (!string.IsNullOrEmpty(label))
                text += $"({label})";
            TreeNode newNode = nodes.Add(text);
            newNode.Tag = new NodeInfo(inXmlNode);
            if (inXmlNode.HasChildNodes)
            {
                XmlNodeList nodeList = inXmlNode.ChildNodes;
                for (int i = 0; i <= nodeList.Count - 1; i++)
                {
                    XmlNode xNode = inXmlNode.ChildNodes[i];
                    AddNode(newNode.Nodes, xNode);
                }
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
                propertyGrid.SelectedObject = new XmlNodeWrapper(node.xml);
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
            System.Collections.IList list = nodes;
            for (int i = list.Count - 1; i >= 0; --i)
            {
                TreeNode node = (TreeNode)list[i];
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
