#region Using directives
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

using Pic.DAL;

using log4net;
#endregion

namespace PicParam
{
    public partial class DocumentTreeView : System.Windows.Forms.TreeView, Pic.DAL.SQLite.TreeInterface
    {
        #region Data members
        protected static readonly ILog _log = LogManager.GetLogger(typeof(DocumentTreeView));
        public bool _preventEventTriggering = false;
        #endregion

        #region Constructor
        public DocumentTreeView()
        {
            try
            {
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DocumentTreeView));
                // get image for tree
                ImageList = new ImageList();
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetAssembly(GetType());
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("CLSDFOLD"))); // 0
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("OPENFOLD"))); // 1
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("COMPONENT"))); // 2
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("DOC"))); // 3
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("DXF"))); // 4
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("PDF"))); // 5
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("STARTPAGE"))); // 6
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("DOWNLOAD"))); // 7
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("AI"))); // 8
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("IMAGE"))); // 9
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("MSWORD"))); // 10
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("MSEXCEL"))); // 11
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("MSPPT"))); // 12
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("WRITER"))); // 13
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("CALC"))); // 14
                ImageList.Images.Add((System.Drawing.Image)(resources.GetObject("ARD"))); // 15

                // events
                AfterExpand += new TreeViewEventHandler(DocumentTreeView_AfterExpand);
                AfterSelect += new TreeViewEventHandler(DocumentTreeView_AfterSelect);

                MouseDown += new MouseEventHandler(DocumentTreeView_MouseDown);

                this.AllowDrop = true;
                ItemDrag += new ItemDragEventHandler(DocumentTreeView_ItemDrag);
                DragEnter += new DragEventHandler(DocumentTreeView_DragEnter);
                DragOver += new DragEventHandler(DocumentTreeView_DragOver);
                DragDrop += new DragEventHandler(DocumentTreeView_DragDrop);
                NodeDropped += new NodeDroppedHandler(DocumentTreeView_NodeDropped);

                // construct tree
                RefreshTree();
                // show tool tips
                ShowNodeToolTips = true;
                // allow drag and drop
                AllowDrop = true;
            }
            catch (Pic.DAL.SQLite.ExceptionDAL ex)
            {
                Debug.Fail(ex.ToString());
                _log.Debug(ex.ToString());
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Debug(ex.ToString());
            }
        }

        #endregion

        #region Drag and drop handling
        void DocumentTreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            // move the dragged node when the left mouse button is used.
            if (e.Button == MouseButtons.Left)
            {
                _draggedNode = e.Item as TreeNode;
                if (null != _draggedNode)
                    DoDragDrop(e.Item, DragDropEffects.Move);
            }
        }

        void DocumentTreeView_DragDrop(object sender, DragEventArgs e)
        {
            if (null == _draggedNode) return;
            // retrieve the client coordinates of the drop location.
            Point targetPoint = PointToClient(new Point(e.X, e.Y));
            // retrieve the node at the drop location.
            TreeNode dropTargetNode = GetNodeAt(targetPoint);
            if (null != dropTargetNode)
            {
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                NodeTag tagTo = dropTargetNode.Tag as NodeTag;
                Pic.DAL.SQLite.TreeNode nodeTo = Pic.DAL.SQLite.TreeNode.GetById(db, tagTo.TreeNode);
                if (!nodeTo.IsDocument)
                    NodeDropped(this, new NodeDroppedArgs(_draggedNode, dropTargetNode));
            }
        }

        void DocumentTreeView_DragOver(object sender, DragEventArgs e)
        {
            if (null == _draggedNode) return;
            // retrieve the client coordinates of the mouse position.
            Point targetPoint = PointToClient(new Point(e.X, e.Y));
            TreeNode targetNode = GetNodeAt(targetPoint);
            if (null != targetNode)
            {
                NodeTag tagTo = targetNode.Tag as NodeTag;
                if (!tagTo.IsDocument)
                {
                    // select the node at the mouse position.
                    SelectedNode = GetNodeAt(targetPoint);
                }
            }
        }

        void DocumentTreeView_DragEnter(object sender, DragEventArgs e)
        {
            if (null == _draggedNode) return;
            // Set the target drop effect to the effect 
            // specified in the ItemDrag event handler.
            e.Effect = e.AllowedEffect;
        }

        void DocumentTreeView_NodeDropped(object sender, NodeDroppedArgs args)
        {
            try
            {
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                // get data treenodes
                NodeTag tagFrom = args._treeNodeFrom.Tag as NodeTag;
                Pic.DAL.SQLite.TreeNode nodeFrom = Pic.DAL.SQLite.TreeNode.GetById(db, tagFrom.TreeNode);
                NodeTag tagTo = args._treeNodeTo.Tag as NodeTag;
                Pic.DAL.SQLite.TreeNode nodeTo = Pic.DAL.SQLite.TreeNode.GetById(db, tagTo.TreeNode);

                // check that nodeTo is not a descendant of nodeFrom
                if (nodeTo.IsDescendantOf(db, nodeFrom))
                {
                    MessageBox.Show(string.Format(PicParam.Properties.Resources.ID_TREEWARNING_CANNOTMOVENODE, nodeFrom.Name, nodeTo.Name)
                        , Application.ProductName
                        , MessageBoxButtons.OK
                        , MessageBoxIcon.Error);
                    return;
                }

                // confirm ?
                if (DialogResult.Yes == MessageBox.Show(
                    string.Format(PicParam.Properties.Resources.ID_TREEWARNING_MOVENODE, nodeFrom.Name, nodeTo.Name)
                    , Application.ProductName
                    , MessageBoxButtons.YesNo
                    , MessageBoxIcon.Warning))
                {
                    // move node in the database
                    nodeFrom.Move(db, nodeTo);
                    // rebuild tree
                    Pic.DAL.SQLite.TreeBuilder builder = new Pic.DAL.SQLite.TreeBuilder();
                    builder.BuildTree(this);
                }
            }
            catch (Pic.DAL.SQLite.ExceptionDAL ex)
            {
                MessageBox.Show(ex.ToString());
                _log.Error(string.Format("Exception caught : {0}", ex.ToString()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                _log.Error(string.Format("Exception caught : {0}", ex.ToString()));
            }
        }
        #endregion

        #region Pic.DAL.SQLite.TreeInterface implementation
        public void Initialize()
        {
            // do not insert anything while designing the UI
            if (this.DesignMode) return;
            // turn off visual updating
            BeginUpdate();
            // clear tree
            Nodes.Clear();
            // insert startpage node
            TreeNode treeNodeStartPage = new TreeNode("Start Page", 6, 6);
            treeNodeStartPage.Tag = new StartPageTag();
            Nodes.Add(treeNodeStartPage);
            // insert download node
            TreeNode treeNodeDownloadPage = new TreeNode("Download", 7, 7);
            treeNodeDownloadPage.Tag = new DownloadPageTag();
            Nodes.Add(treeNodeDownloadPage);
        }
        public void Finish()
        {
            // turn on visual updating
            EndUpdate();
        }
        public object InsertTreeNode(object parentObj, Pic.DAL.SQLite.TreeNode node)
        {
            TreeNode treeNodeNew = new TreeNode(node.Name, GetImageIndex(node), GetSelectedImageIndex(node));
            treeNodeNew.Tag = new NodeTag(
                node.IsDocument ? NodeTag.NodeType.NT_DOCUMENT : NodeTag.NodeType.NT_TREENODE
                , node.ID
                , node.Name
                , node.Description
                , node.Thumbnail.GetImage());
            treeNodeNew.ToolTipText = node.Description;
            treeNodeNew.ContextMenuStrip = node.IsDocument ? GetLeafMenu() : GetBranchMenu();
            TreeNode treeNodeParent = parentObj as TreeNode;
            
            if (null == treeNodeParent)
                Nodes.Add(treeNodeNew);
            else
            {
                // find index
                int index = 0;
                foreach (TreeNode tn in treeNodeParent.Nodes)
                {
                    NodeTag tag = tn.Tag as NodeTag;
                    if (tag == null) continue;
                    if (string.Compare(tn.Text, node.Name) == -1)
                        ++index;
                }
                treeNodeParent.Nodes.Insert(index, treeNodeNew);
            }
            return treeNodeNew;
        }

        public void InsertDummyNode(object parentObj)
        {
            TreeNode treeNodeParent = parentObj as TreeNode;
            TreeNode dummyTreeNode = new TreeNode("_DUMMY_", 0, 0);
            treeNodeParent.Nodes.Add(dummyTreeNode);
        }

        public void AskRootNode()
        {
            FormCreateBranch form = new FormCreateBranch();
            if (DialogResult.OK == form.ShowDialog())
            {
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                Pic.DAL.SQLite.TreeNode.CreateNew(db, form.BranchName, form.BranchDescription, form.BranchImage);
            }
        }
        #endregion

        #region Event handlers
        public void CollapseRoot()
        {
            if (Nodes.Count > 1 && 0 == string.Compare(Nodes[1].Text, "Root")) {
                Nodes[1].Nodes.Clear();
                Nodes[1].Nodes.Add(new TreeNode("_DUMMY_", 0, 0));
                Nodes[1].Collapse();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void CollapseRootChildrens()
        {
            // collapse each child of the "Root" node
            if (Nodes.Count > 1 && 0 == string.Compare(Nodes[1].Text, "Root"))
            {
                // root node
                TreeNode rootNode = Nodes[1];
                // collapse each node
                foreach (TreeNode tn in rootNode.Nodes)
                    tn.Collapse();
                // select root node
                if (null != rootNode)
                    SelectedNode = rootNode;
            }
        }

        private void DocumentTreeView_AfterExpand(object sender, TreeViewEventArgs e)
        {
            try
            {
                if (e.Node.Nodes.Count == 1 && 0 == string.Compare(e.Node.Nodes[0].Text, "_DUMMY_"))
                {   // this node has not yet been populated
                    bool singlethreaded = true;
                    if (singlethreaded)
                        PopulateChildren(e.Node);
                    else // multithreaded
                    {
                        // launch a thread to get the data
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            // load the data into the tree view (on the UI thread)
                            BeginInvoke((Action)delegate
                            {
                                PopulateChildren(e.Node);
                            });
                        });
                    }
                }
                // select node
                SelectedNode = e.Node;
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Error(ex.ToString());
            }
        }

        private void PopulateChildren(TreeNode parent)
        {
            // remove _DUMMY_ tree node
            parent.Nodes.Clear();
            // populate with actual nodes 
            NodeTag tag = parent.Tag as NodeTag;
            Pic.DAL.SQLite.TreeBuilder treeBuilder = new Pic.DAL.SQLite.TreeBuilder();
            treeBuilder.PopulateChildren(this, parent, tag.TreeNode);
        }

        void DocumentTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
                NodeTag currentTag = GetCurrentTag();
                if (null == currentTag)
                {
                    StartPageTag startPageTag = e.Node.Tag as StartPageTag;
                    if (null != StartPageSelected && null != startPageTag)
                        StartPageSelected(this);
                    DownloadPageTag downloadPageTag = e.Node.Tag as DownloadPageTag;
                    if (null != DownloadPageSelected && null != downloadPageTag)
                        DownloadPageSelected(this);
                }
                else if (null != SelectionChanged && !_preventEventTriggering)
                    SelectionChanged(this, new NodeEventArgs(currentTag.TreeNode, currentTag.Type), currentTag.Name);
            }
            catch (Pic.Plugin.PluginException /*ex*/)
            {
                // do nothing
                // -> users sees message on plugin viewer control
            }
            catch (System.Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }

        void DocumentTreeView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                TreeNode tn = GetNodeAt(e.Location);
                SelectedNode = tn;
            }
        }
        #endregion

        #region Handlers
        public void OnSelectionChanged(object sender, NodeEventArgs e, string name)
        {
            PopulateAndSelectNode(new NodeTag(e.Type, e.Node)); 
        }
        public void OnTreeNodeCreated(object sender, NodeEventArgs e)
        {
            Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
            Pic.DAL.SQLite.TreeNode nodeNew = Pic.DAL.SQLite.TreeNode.GetById(db, e.Node);
            TreeNode parentNode = FindNode(null, new NodeTag(NodeTag.NodeType.NT_TREENODE, nodeNew.ParentNodeID.Value));
            // clear
            parentNode.Nodes.Clear();
            // insert dummy node
            parentNode.Nodes.Add("_DUMMY_");
            // collapse
            parentNode.Collapse();
            // reload
            parentNode.Expand();
        }
        #endregion

        #region Helpers
        internal NodeTag GetCurrentTag()
        {
            TreeNode currentNode = this.SelectedNode;
            if (null == currentNode)
                throw new Exception("No node selected");
            return currentNode.Tag as NodeTag;
        }
        #endregion

        #region Tree filling methods
        public void RefreshTree()
        {
            // do not attempt to build tree when in design mode
            if (this.DesignMode) return;
            try
            {
                Pic.DAL.SQLite.TreeBuilder treeBuilder = new Pic.DAL.SQLite.TreeBuilder();
                treeBuilder.BuildTree(this);

                if (this.Nodes.Count > 0)
                    SelectedNode = this.Nodes[0];

            }
            catch (System.Exception ex)
            {
                _log.Error(string.Format("Exception caught: {0}", ex.ToString()));
            }
        }

        public void SelectNode(NodeTag tag)
        {
            SelectedNode = FindNode(null, tag);
            if (null != SelectedNode)
                SelectedNode.EnsureVisible();
        }

        public void PopulateAndSelectNode(NodeTag tag)
        {
            TreeNode nodeSelected = FindNode(null, tag);
            if (null == nodeSelected)
            {
                _log.Error(string.Format("Failed to retrieve valid node from NodeTag {0}", tag.ToString()));
                // retrieve db node
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                Pic.DAL.SQLite.TreeNode tn = Pic.DAL.SQLite.TreeNode.GetById(db, tag.TreeNode);
                // insert all needed parents and node
                nodeSelected = InsertNodeAndParents(tn, db);
                if (null == nodeSelected)
                {
                    _log.Error(string.Format("Failed to retrieve valid node from NodeTag {0}", tag.ToString()));
                    return; // -> complete failure
                }
            }
            _preventEventTriggering = true;
            if (null != nodeSelected)
            {
                // ################## --> needed to update tree
                nodeSelected.Expand();
                // ################## --> needed to update tree
                SelectedNode = nodeSelected;
            }
            _preventEventTriggering = false;
        }

        // recursive insertion mathod
        public TreeNode InsertNodeAndParents(Pic.DAL.SQLite.TreeNode tn, Pic.DAL.SQLite.PPDataContext db)
        {
            // failed !
            if (null == tn)  return null;

            TreeNode parentNode = FindNode(null, new NodeTag(NodeTag.NodeType.NT_TREENODE, tn.ParentNodeID.Value));
            if (null == parentNode)
                parentNode = InsertNodeAndParents(Pic.DAL.SQLite.TreeNode.GetById(db, tn.ParentNodeID.Value), db);
            Pic.DAL.SQLite.TreeBuilder treeBuilder = new Pic.DAL.SQLite.TreeBuilder();
            // remove _DUMMY_ tree node
            parentNode.Nodes.Clear();
            treeBuilder.PopulateChildren(this, parentNode, tn.ParentNodeID.Value);

            return FindNode(null, new NodeTag(NodeTag.NodeType.NT_TREENODE, tn.ID));
        }

        protected TreeNode FindNode(TreeNode node, NodeTag tag)
        {
            // check with node itself
            if (null != node)
            {
                NodeTag tagNode = node.Tag as NodeTag;
                if (null != tagNode && tagNode.Equals(tag))
                    return node;
            }
            // check with child nodes
            TreeNodeCollection tnCollection = null == node ? Nodes : node.Nodes;
            foreach (TreeNode tn in tnCollection)
            {
                TreeNode tnResult = FindNode(tn, tag);
                if (null != tnResult)
                    return tnResult;
            }
            return null;
        }

        protected int GetImageIndex(Pic.DAL.SQLite.TreeNode tn)
        {
            if (tn.IsDocument)
            {
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                Pic.DAL.SQLite.Document doc = tn.Documents(db)[0];
                string docTypeName = doc.DocumentType.Name.ToLower();

                Dictionary<string, int> format2iconDictionnary = new Dictionary<string, int>()
                {
                    {"parametric component" , 2},
                    {"treeDim des"          , 3},
                    {"autodesk dxf"         , 4},
                    {"adobe acrobat"        , 5},
                    {"adobe illustrator"    , 8},
                    {"raster image"         , 9},
                    {"ms word"              , 10},
                    {"ms excel"             , 11},
                    {"ms powerpoint"        , 12},
                    {"open office write"    , 13},
                    {"open office calc"     , 14},
                    {"artioscad"            , 15}
                };

                if (format2iconDictionnary.ContainsKey(docTypeName))
                    return format2iconDictionnary[docTypeName];
                else
                    return 0;
            }
            else
                return 0;
        }
        protected int GetSelectedImageIndex(Pic.DAL.SQLite.TreeNode tn)
        {
            if (tn.IsDocument)
                return GetImageIndex(tn);
            else
                return 1;
        }
        #endregion

        #region Context menu creation
        protected ContextMenuStrip GetBranchMenu()
        {
            if (null == _branchMenu)
            {
                // create the ContextMenuStrip
                _branchMenu = new ContextMenuStrip();
                // create some menu items
                // -- insert new branch
                ToolStripMenuItem insertNewBranchLabel = new ToolStripMenuItem();
                insertNewBranchLabel.Text = PicParam.Properties.Resources.ID_TREEMENUNEWBRANCH;
                insertNewBranchLabel.Click += new EventHandler(insertNewBranchLabel_Click);
                // -- insert new document
                ToolStripMenuItem insertNewDocumentLabel = new ToolStripMenuItem();
                insertNewDocumentLabel.Text = PicParam.Properties.Resources.ID_TREEMENUNEWDOCUMENT;
                insertNewDocumentLabel.Click += new EventHandler(insertNewDocumentLabel_Click);
                // -- separator 1
                System.Windows.Forms.ToolStripSeparator separator1 = new System.Windows.Forms.ToolStripSeparator();
                // -- backup branch
                ToolStripMenuItem backupBranchLabel = new ToolStripMenuItem();
                backupBranchLabel.Text = string.Format(PicParam.Properties.Resources.ID_TREEMENUBACKUP, "");
                backupBranchLabel.Click += new EventHandler(backupNode);
                // -- separator 2
                System.Windows.Forms.ToolStripSeparator separator2 = new System.Windows.Forms.ToolStripSeparator();
                // -- rename branch
                ToolStripMenuItem renameBranchLabel = new ToolStripMenuItem();
                renameBranchLabel.Text = string.Format(PicParam.Properties.Resources.ID_TREEMENURENAME, "");
                renameBranchLabel.Click += new EventHandler(renameNode);
                // -- separator 3
                System.Windows.Forms.ToolStripSeparator separator3 = new System.Windows.Forms.ToolStripSeparator();
                // -- delete branch
                ToolStripMenuItem deleteLabel = new ToolStripMenuItem();
                deleteLabel.Text = PicParam.Properties.Resources.ID_TREEMENUDELETE;
                deleteLabel.Click += new EventHandler(deleteNode);

                // add the menu items to the menu.
                _branchMenu.Items.AddRange(
                    new ToolStripItem[]{
                        insertNewBranchLabel
                        , insertNewDocumentLabel
                        , separator1
                        , backupBranchLabel
                        , separator2
                        , renameBranchLabel
                        , separator3
                        , deleteLabel
                    }
                    );
            }
            return _branchMenu;
        }

        protected ContextMenuStrip GetLeafMenu()
        {
            if (null == _leafMenu)
            {
                // create the ContextMenuStrip
                _leafMenu = new ContextMenuStrip();
                // create some menu items
                // -> rename
                ToolStripMenuItem renameBranchLabel = new ToolStripMenuItem();
                renameBranchLabel.Text = string.Format(PicParam.Properties.Resources.ID_TREEMENURENAME, "");
                renameBranchLabel.Click += new EventHandler(renameNode);
                // -> separator1
                System.Windows.Forms.ToolStripSeparator separator1 = new System.Windows.Forms.ToolStripSeparator();
                // -> delete
                ToolStripMenuItem deleteLabel = new ToolStripMenuItem();
                deleteLabel.Text = PicParam.Properties.Resources.ID_TREEMENUDELETE;
                deleteLabel.Click += new EventHandler(deleteNode);
                // add the menu items to the menu.
                _leafMenu.Items.AddRange(
                    new ToolStripItem[]{
                        renameBranchLabel
                        , separator1
                        , deleteLabel
                    }
                    );
            }
            return _leafMenu;
        }
        #endregion

        #region Context menu event handlers
        private void deleteNode(object sender, EventArgs e)
        {
            try
            {
                // retrieve database node
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                NodeTag tag = GetCurrentTag();
                Pic.DAL.SQLite.TreeNode tn = Pic.DAL.SQLite.TreeNode.GetById(db, tag.TreeNode);
                // prepare warning message
                string message = string.Empty;
                if (tn.IsDocument)
                    message = string.Format(PicParam.Properties.Resources.ID_TREEWARNING_DELETEDOCUMENT, tn.Name);
                else
                    message = string.Format(PicParam.Properties.Resources.ID_TREEWARNING_DELETEBRANCH, tn.Name);
                // show warning message and proceed with node deletion if necessary
                if (DialogResult.Yes == MessageBox.Show(message, Application.ProductName, MessageBoxButtons.YesNo))
                {
                    // delete from data base
                    tn.Delete(db, true, null);
                    // delete from tree control
                    TreeNode treeNode = FindNode(null, tag);
                    treeNode.Remove();
                }
            }
            catch (System.Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Debug(ex.ToString());
            }
        }

        private void backupNode(object sender, EventArgs e)
        {
            try
            {
                // get node
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                NodeTag tag = GetCurrentTag();
                Pic.DAL.SQLite.TreeNode tn = Pic.DAL.SQLite.TreeNode.GetById(db, tag.TreeNode);
                // get backup file name
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.Filter = "Backup file (*.zip)|*.zip|"  + 
                            "All files (*.*)|*.*";
                dlg.FilterIndex = 0;
                dlg.Title = string.Format("Backup branch {0}", tn.Name);
                dlg.FileName = string.Format("{0}.zip", tn.Name);

                if (DialogResult.OK == dlg.ShowDialog())
                {
                    List<string> treeNodePath = tn.GetTreeNodePath(db);
                    // backup branch
                    FormWorkerThreadTask.Execute(new TPTBackupBranch(treeNodePath, dlg.FileName));
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Debug(ex.ToString());
            }
        }

        private void renameNode(object sender, EventArgs e)
        {
            try
            {
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                NodeTag tag = GetCurrentTag();
                Pic.DAL.SQLite.TreeNode tn = Pic.DAL.SQLite.TreeNode.GetById(db, tag.TreeNode);

                FormRenameNode form = new FormRenameNode();
                form.NodeName = tn.Name;
                form.NodeDescription = tn.Description;
                form.Image = tn.Thumbnail.GetImage();
                if (DialogResult.OK == form.ShowDialog())
                {
                    tn.Name = form.NodeName;
                    tn.Description = form.NodeDescription;
                    db.SubmitChanges();

                    if (form.HasValidThumbnailPath)
                        Pic.DAL.SQLite.TreeNode.ReplaceThumbnail(tag.TreeNode, form.ThumbnailPath);

                    TreeNode treeNode = FindNode(null, tag);
                    treeNode.Text = form.NodeName;
                    treeNode.ToolTipText = form.NodeDescription;
                }
            }
            catch (System.Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Debug(ex.ToString());
            }
        }

        private void insertNewDocumentLabel_Click(object sender, EventArgs e)
        {
            try
            {
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();

                NodeTag tag = GetCurrentTag();
                if (null == tag || tag.IsDocument)
                    throw new Exception("Invalid TreeNode tag");
                Pic.DAL.SQLite.TreeNode treeNode = Pic.DAL.SQLite.TreeNode.GetById(db, tag.TreeNode);

                // force creation of a profile if no profile exist
                Pic.DAL.SQLite.CardboardProfile[] cardboardProfiles = Pic.DAL.SQLite.CardboardProfile.GetAll(db);
                if (cardboardProfiles.Length == 0)
                {
                    FormEditProfiles dlgProfile = new FormEditProfiles();
                    dlgProfile.ShowDialog();
                }
                cardboardProfiles = Pic.DAL.SQLite.CardboardProfile.GetAll(db);
                if (cardboardProfiles.Length == 0)
                    return;

                // show dialog
                FormCreateDocument dlg = new FormCreateDocument();
                dlg.TreeNode = tag;
                if (DialogResult.OK == dlg.ShowDialog())
                {
                    // insert document in treeview
                    List<Pic.DAL.SQLite.TreeNode> tnodes = Pic.DAL.SQLite.TreeNode.GetByDocumentId(db, dlg.DocumentID);
                    // (re)populate parent node
                    PopulateChildren(SelectedNode);
                    if (dlg.OpenInsertedDocument)
                    {
                        // select current node
                        NodeTag newDocTag = new NodeTag(NodeTag.NodeType.NT_DOCUMENT, tnodes[0].ID);
                        SelectedNode = FindNode(null, newDocTag);  // <- for some reasons, this does not work
                        // however, we can force document opening with the following line
                        SelectionChanged(this, new NodeEventArgs(newDocTag.TreeNode, newDocTag.Type), newDocTag.Name);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Error(ex.ToString());
            }
        }

        private void insertNewBranchLabel_Click(object sender, EventArgs e)
        {
            try
            {
                // get current tree node
                NodeTag tag = GetCurrentTag();
                if (null == tag || tag.IsDocument)
                    throw new Exception("Invalid branch tag");
                // show dialog
                FormCreateBranch dlg = new FormCreateBranch();
                if (DialogResult.OK == dlg.ShowDialog())
                {
                    Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                    // get parent node
                    Pic.DAL.SQLite.TreeNode parentNode = Pic.DAL.SQLite.TreeNode.GetById(db, tag.TreeNode);
                    // create new branch
                    Pic.DAL.SQLite.TreeNode nodeNew = parentNode.CreateChild(db, dlg.BranchName, dlg.BranchDescription, dlg.BranchImage);
                    // insert branch node
                    InsertTreeNode(SelectedNode, nodeNew);
                    // select current node
                    SelectedNode = FindNode(null, tag);
                }
            }
            catch (System.Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Debug(ex.ToString());
            }
        }
        #endregion

        #region Delegates
        public delegate void StartPageSelectHandler(object sender);
        public delegate void DownloadPageSelectHandler(object sender);
        public delegate void SelectionChangedHandler(object sender, NodeEventArgs e, string name);
        public delegate void LeafSelectHandler(object sender, NodeEventArgs e);
        public delegate void NodeDroppedHandler(object sender, NodeDroppedArgs e);
        #endregion

        #region Events
        public event StartPageSelectHandler StartPageSelected;
        public event DownloadPageSelectHandler DownloadPageSelected;
        public event SelectionChangedHandler SelectionChanged;
        public event NodeDroppedHandler NodeDropped;
        #endregion

        #region Data members
        private ContextMenuStrip _branchMenu;
        private ContextMenuStrip _leafMenu;
        private TreeNode _draggedNode;
        #endregion
    }

    #region Event argument classes : NodeEventArgs
    public class NodeEventArgs : EventArgs
    {
        public NodeEventArgs(int treeNodeId, NodeTag.NodeType type) { _treeNodeId = treeNodeId; _type = type; }
        public int Node { get { return _treeNodeId; } }
        public NodeTag.NodeType Type { get { return _type; } }
        private int _treeNodeId;
        private NodeTag.NodeType _type;
    }
    #endregion

    #region Event argument classes : NodeDroppedArgs
    public class NodeDroppedArgs : EventArgs
    {
        public NodeDroppedArgs(TreeNode nodeFrom, TreeNode nodeTo)
        { _treeNodeFrom = nodeFrom; _treeNodeTo = nodeTo; }
        public TreeNode _treeNodeFrom, _treeNodeTo;
    }
    #endregion

    #region Class NodeTag to be used as a TreeNode tag
    public class StartPageTag { }
    public class DownloadPageTag { }
    public class NodeTag
    {
        #region Enums
        public enum NodeType
        {
            NT_TREENODE
            , NT_DOCUMENT
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Basic constructor with no cached values 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        public NodeTag(NodeType type, int id)
        {
            _id = id;
            _type = type;
        }
        public NodeTag(NodeType type, int id, string name, string description, Image thumbnail)
        {
            _id = id;
            _type = type;
            _name = name;
            _description = description;
            _thumbnail = thumbnail;
        }
        #endregion

        #region Accessing inner content
        public NodeType Type { get { return _type; } }
        public bool IsDocument { get { return NodeType.NT_DOCUMENT == _type; } }
        public bool IsTreeNode { get { return NodeType.NT_TREENODE == _type; } }
        /// <summary>
        /// TreeNode id
        /// </summary>
        public int TreeNode
        {
            get
            {
                return _id;
            }
        }
        /// <summary>
        /// Thumbnail
        /// </summary>
        public Image GetThumbnail()
        {
            if (null == _thumbnail)
            {
                Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                Pic.DAL.SQLite.TreeNode node = Pic.DAL.SQLite.TreeNode.GetById(db, _id);
                Image tempImage = node.Thumbnail.GetImage();
                Pic.DAL.SQLite.Thumbnail.Annotate(tempImage, node.Name);
                _thumbnail = tempImage;
            }
            return _thumbnail;
        }
        /// <summary>
        /// Name
        /// </summary>
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(_name))
                {
                    Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                    Pic.DAL.SQLite.TreeNode node = Pic.DAL.SQLite.TreeNode.GetById(db, _id);
                    _name = node.Name;
                }
                return _name;
            }
        }
        /// <summary>
        /// Description
        /// </summary>
        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(_description))
                {
                    Pic.DAL.SQLite.PPDataContext db = new Pic.DAL.SQLite.PPDataContext();
                    Pic.DAL.SQLite.TreeNode node = Pic.DAL.SQLite.TreeNode.GetById(db, _id);
                    _description = node.Description;
                }
                return _description;
            }
        }
        #endregion

        #region System.Object override
        public override bool Equals(object obj)
        {
            NodeTag nodeTag = obj as NodeTag;
            return (_id == nodeTag._id) /*&& (_type == nodeTag._type)*/;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        #endregion

        #region Data members
        private int _id;
        private NodeType _type;
        // Cached values
        // name / description
        private string _name, _description;
        // thumbnail image
        private Image _thumbnail;
        #endregion
    }
    #endregion // NodeTag
}
