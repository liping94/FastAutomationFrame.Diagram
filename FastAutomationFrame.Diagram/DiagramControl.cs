//======================================================================
//
//        Copyright (C) 2020-2021 个人软件工作室    
//        All rights reserved
//
//        filename :DiagramControl.cs
//        description :
//
//        created by 张恭亮 at  2020/9/22 10:55:28
//
//======================================================================

using FastAutomationFrame.Diagram.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace FastAutomationFrame.Diagram
{
    public class DiagramControl : Panel
    {
        #region Events and delegates

        public event EventHandler<DeletingEventArgs> OnElementDeleting;
        public event EventHandler<EventArgs> OnElementDeleted;
        public event EventHandler<SelectElementChangedEventArgs> OnSelectElementChanged;

        #endregion

        #region Fields

        private bool _leftMouse = false;
        /// <summary>
        /// 用于计算鼠标移动距离
        /// </summary>
        private DiagramPoint _stratPoint = new DiagramPoint();
        private bool tracking = false;
        private GlobalHook hook;
        protected Size gridSize = new Size(10, 10);
        protected Proxy proxy;
        protected Entity selectedEntity;
        protected Entity hoveredEntity;
        protected ShapeCollection shapes;
        protected ConnectionCollection connections;
        protected Random rnd;

        #endregion

        #region Fields && Properties

        protected bool showGrid = false;
        [Description("是否显示点网格"), Category("Layout")]
        public bool ShowGrid
        {
            get { return showGrid; }
            set { showGrid = value; Invalidate(true); }
        }

        protected Color lineColor = Color.Silver;
        [Browsable(true), Description("连线默认颜色"), Category("Layout")]
        public Color LineColor
        {
            get { return lineColor; }
            set { lineColor = value; }
        }

        protected Color lineSelectedColor = Color.Green;
        [Browsable(true), Description("连线选中颜色"), Category("Layout")]
        public Color LineSelectedColor
        {
            get { return lineSelectedColor; }
            set { lineSelectedColor = value; }
        }

        protected Color lineHoveredColor = Color.Blue;
        [Browsable(true), Description("连线悬停颜色"), Category("Layout")]
        public Color LineHoveredColor
        {
            get { return lineHoveredColor; }
            set { lineHoveredColor = value; }
        }

        private DiagramPoint _viewOriginPoint = new DiagramPoint(0, 0);
        [Browsable(true), Description("视觉原点，左上角坐标"), Category("Layout")]
        public DiagramPoint ViewOriginPoint
        {
            get { return _viewOriginPoint; }
            set { _viewOriginPoint = value; }
        }

        [Browsable(false)]
        public ShapeCollection ShapeCollection => shapes;

        [Browsable(false)]
        public ConnectionCollection Connections => connections;

        #endregion

        #region Constructor
        /// <summary>
        /// Default ctor
        /// </summary>
        public DiagramControl()
        {
            SetStyle(ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
            this.AllowDrop = true;

            shapes = new ShapeCollection();
            connections = new ConnectionCollection();
            rnd = new Random();
            proxy = new Proxy(this);

            hook = new GlobalHook();
            hook.KeyDown += new KeyEventHandler(hook_KeyDown);
            bool b = hook.Start();
        }

        #endregion

        #region Methods

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);
            object objData = drgevent.Data.GetData(typeof(DragItem));
            if (objData != null)
            {
                DragItem dragItem = objData as DragItem;
                ShapeBase shapeBase = (ShapeBase)Activator.CreateInstance(dragItem.Shape.GetType());
                this.AddShape(shapeBase);
                Point p1 = Control.MousePosition;
                Point p2 = this.PointToScreen(new Point(0, 0));
                shapeBase.X = (p1.X - p2.X - ViewOriginPoint.GetPoint().X - 1) / 2;
                shapeBase.Y = p1.Y - p2.Y - ViewOriginPoint.GetPoint().Y;
            }
        }

        public void Save(string savePath)
        {
            SaveDataInfo data = this;
            XmlSerializer xs = new XmlSerializer(data.GetType());
            using (Stream stream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                xs.Serialize(stream, data);
            }
            SignXmlHelper.SignXml(savePath);
        }

        public bool Import(string DGPath, out string msg, bool needShapeData = true, bool needControlData = false)
        {
            if (!SignXmlHelper.VerifyXml(DGPath))
            {
                msg = "文件已被更改！";
                return false;
            }

            msg = "";
            XmlSerializer xs = new XmlSerializer(typeof(SaveDataInfo));
            using (Stream stream = new FileStream(DGPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SaveDataInfo dataInfo = (xs.Deserialize(stream) as SaveDataInfo);
                if (needShapeData)
                {
                    this.CopyShapes(dataInfo);
                }

                if (needControlData)
                {
                    this.CopyControlParams(dataInfo);
                }
            }
            return true;
        }

        public void CopyControlParams(SaveDataInfo dataInfo)
        {
            this.ViewOriginPoint = dataInfo.ViewOriginPoint;
            this.LineHoveredColor = dataInfo.LineHoveredColor;
            this.LineSelectedColor = dataInfo.LineSelectedColor;
            this.LineColor = dataInfo.LineColor;
            this.BackColor = dataInfo.BackColor;
            this.ShowGrid = dataInfo.ShowGrid;
        }

        public void CopyShapes(SaveDataInfo dataInfo)
        {
            dataInfo.Shapes.ForEach(shape =>
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Assembly selectAssemblie = assemblies.FirstOrDefault((Func<Assembly, bool>)(assemblie => assemblie.FullName == shape.AssemblyInfo));
                Type shapeType = selectAssemblie.GetType(shape.Type);
                Object[] constructParms = new object[] { };  //构造器参数
                ShapeBase createShape = (ShapeBase)Activator.CreateInstance(shapeType, constructParms);
                this.AddShape(createShape);
                createShape.ObjectID = shape.ObjectID;
                createShape.X = shape.X;
                createShape.Y = shape.Y;
                createShape.Text = shape.Text;
                createShape.BackGroundColor = shape.BackGroundColor;
                createShape.BoderColor = shape.BoderColor;
                createShape.BoderSelectedColor = shape.BoderSelectedColor;
                createShape.EnableBottomSourceConnector = shape.EnableBottomSourceConnector;
                createShape.EnableLeftSourceConnector = shape.EnableLeftSourceConnector;
                createShape.EnableRightSourceConnector = shape.EnableRightSourceConnector;
                createShape.EnableTopSourceConnector = shape.EnableTopSourceConnector;
                createShape.EnableBottomTargetConnector = shape.EnableBottomTargetConnector;
                createShape.EnableLeftTargetConnector = shape.EnableLeftTargetConnector;
                createShape.EnableRightTargetConnector = shape.EnableRightTargetConnector;
                createShape.EnableTopTargetConnector = shape.EnableTopTargetConnector;
                createShape.ShowBorder = shape.ShowBorder;
            });

            dataInfo.Connections.ForEach(connection =>
            {
                ShapeBase shapeFrom = null;
                ShapeBase shapeTo = null;
                foreach (ShapeBase shape in shapes)
                {
                    if (connection.FromContainEntityObjectID == shape.ObjectID)
                    {
                        shapeFrom = shape;
                    }

                    if (connection.ToContainEntityObjectID == shape.ObjectID)
                    {
                        shapeTo = shape;
                    }

                    if (shapeFrom != null && shapeTo != null)
                    {
                        break;
                    }
                }

                if (shapeFrom == null || shapeTo == null)
                {
                    return;
                }

                this.AddConnection(shapeFrom.Connectors[connection.FromContainEntityIndex], shapeTo.Connectors[connection.ToContainEntityIndex]);
            });
        }

        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            base.OnDragEnter(drgevent);
            if (drgevent.Data.GetDataPresent(typeof(DragItem)))
            {
                drgevent.Effect = DragDropEffects.Copy;
            }
        }

        private void hook_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectElement();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _leftMouse = e.Button == MouseButtons.Left;
            _stratPoint = e.Location;

            int entityTYpe = -1;//0:ShapeBase;1:ShapeBase.connectors;2:connections;3:connections.From;4:connections.To.
            Entity hoveredentity = shapes.Cast<ShapeBase>().FirstOrDefault(f =>
            {
                if (f.Hit(e.Location))
                {
                    return true;
                }
                return false;
            });

            if (hoveredentity == null && selectedEntity != null && selectedEntity is ShapeBase)
            {
                Connector connector = (selectedEntity as ShapeBase).HitConnector(e.Location);
                if (connector != null)
                {
                    Point point = e.Location;
                    point.Offset(-this.ViewOriginPoint.GetPoint().X, -this.ViewOriginPoint.GetPoint().Y);

                    Connection connection = this.AddConnection(connector.Point, point);
                    connection.From.ContainEntity = connector.ContainEntity;
                    connection.From.ConnectorsIndexOfContainEntity = connector.ConnectorsIndexOfContainEntity;
                    UpdateSelected(connection.To);
                    connector.AttachConnector(connection.From);
                    tracking = true;
                    Invalidate(true);
                    return;
                }
            }

            if (hoveredentity != null)
            {
                tracking = true;
                OnSelectChanged(hoveredentity, new SelectElementChangedEventArgs() { CurrentEntity = hoveredentity, PreviousEntity = selectedEntity });
            }
            else
            {
                hoveredentity = connections.Cast<Connection>().FirstOrDefault(f =>
                {
                    if (f.Hit(e.Location))
                    {
                        entityTYpe = 2;
                        return true;
                    }
                    if (f.From.Hit(e.Location))
                    {
                        entityTYpe = 3;
                        return true;
                    }
                    if (f.To.Hit(e.Location))
                    {
                        entityTYpe = 4;
                        return true;
                    }
                    return false;
                });

                if (entityTYpe == 3)
                {
                    hoveredentity = ((Connection)hoveredentity).From;
                    tracking = true;
                }
                else if (entityTYpe == 4)
                {
                    hoveredentity = ((Connection)hoveredentity).To;
                    tracking = true;
                }
                else if (entityTYpe == 2)
                {
                    OnSelectChanged(hoveredentity, new SelectElementChangedEventArgs() { CurrentEntity = hoveredentity, PreviousEntity = selectedEntity });
                }
            }

            if (hoveredentity == null)
            {
                OnSelectChanged(this.proxy, new SelectElementChangedEventArgs() { CurrentEntity = hoveredentity, PreviousEntity = selectedEntity });
            }
            UpdateSelected(hoveredentity);
            Invalidate(true);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _leftMouse = false;

            if (tracking)
            {
                Point p = new Point(e.X, e.Y);

                if (typeof(Connector).IsInstanceOfType(selectedEntity))
                {
                    Connector con;
                    for (int k = 0; k < shapes.Count; k++)
                    {
                        if ((con = shapes[k].HitConnector(p)) != null)
                        {
                            con.AttachConnector((selectedEntity as Connector));
                            (selectedEntity as Connector).ContainEntity = con.ContainEntity;
                            (selectedEntity as Connector).ConnectorsIndexOfContainEntity = con.ConnectorsIndexOfContainEntity;
                            con.hovered = false;
                            tracking = false;
                            return;
                        }
                    }

                  (selectedEntity as Connector).Release();
                    this.DeleteElement((selectedEntity as Connector).ContainEntity);
                }
                else
                {

                }
                tracking = false;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (tracking)
            {
                selectedEntity.Move(new Point(e.X - _stratPoint.GetPoint().X, e.Y - _stratPoint.GetPoint().Y));
                if (typeof(Connector).IsInstanceOfType(selectedEntity))
                {
                    for (int k = 0; k < shapes.Count; k++)
                    {
                        shapes[k].HitConnector(e.Location);
                    }
                }
            }
            else if (_leftMouse)
            {
                _viewOriginPoint = new DiagramPoint(_viewOriginPoint.GetPoint().X + e.X - _stratPoint.GetPoint().X, _viewOriginPoint.GetPoint().Y + e.Y - _stratPoint.GetPoint().Y);
            }

            int entityTYpe = -1;//0:ShapeBase;1:ShapeBase.connectors;2:connections;3:connections.From;4:connections.To.
            Entity hoveredentity = shapes.Cast<Entity>().FirstOrDefault(f => f.Hit(e.Location));
            if (hoveredentity == null)
            {
                hoveredentity = connections.Cast<Connection>().FirstOrDefault(f =>
                {
                    if (f.Hit(e.Location))
                    {
                        entityTYpe = 2;
                        return true;
                    }
                    if (f.From.Hit(e.Location))
                    {
                        entityTYpe = 3;
                        return true;
                    }
                    if (f.To.Hit(e.Location))
                    {
                        entityTYpe = 4;
                        return true;
                    }
                    return false;
                });
            }

            if (entityTYpe == 3)
                hoveredentity = ((Connection)hoveredentity).From;
            if (entityTYpe == 4)
                hoveredentity = ((Connection)hoveredentity).To;

            UpdateHovered(hoveredentity);

            _stratPoint = e.Location;
            Invalidate(true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Graphics g = e.Graphics;

            if (showGrid)
                ControlPaint.DrawGrid(g, this.ClientRectangle, gridSize, this.BackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);//解决闪烁

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            for (int k = 0; k < connections.Count; k++)
            {
                connections[k].Paint(g);
                connections[k].From.Paint(g);
                connections[k].To.Paint(g);
            }

            for (int k = 0; k < shapes.Count; k++)
            {
                shapes[k].Paint(g);
            }

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);//解决闪烁 
        }

        public void DeleteElement(Entity entity)
        {
            if (entity == null) return;

            if (entity is ShapeBase)
            {
                DeletingEventArgs deletingEventArgs = new DeletingEventArgs();
                OnDeleting(entity, deletingEventArgs);

                if (!deletingEventArgs.Cancel)
                {
                    this.shapes.Remove(entity as ShapeBase);
                    this.Invalidate(true);
                    OnDeleted(entity, EventArgs.Empty);
                }
            }
            else if (entity is Connection)
            {
                DeletingEventArgs deletingEventArgs = new DeletingEventArgs();
                OnDeleting(entity, deletingEventArgs);

                if (!deletingEventArgs.Cancel)
                {
                    this.connections.Remove(entity as Connection);
                    this.Invalidate(true);
                    OnDeleted(entity, EventArgs.Empty);
                }
            }
        }
        public ShapeBase AddShape(ShapeBase shape)
        {
            shapes.Add(shape);
            shape.Site = this;
            this.Invalidate(true);
            return shape;
        }

        public Connection AddConnection(Connector from, Connector to)
        {
            Connection con = this.AddConnection(from.Point, to.Point);
            con.From.ContainEntity = from.ContainEntity;
            con.From.ConnectorsIndexOfContainEntity = from.ConnectorsIndexOfContainEntity;
            con.To.ContainEntity = to.ContainEntity;
            con.To.ConnectorsIndexOfContainEntity = to.ConnectorsIndexOfContainEntity;
            con.Site = this;
            from.AttachConnector(con.From);
            to.AttachConnector(con.To);

            return con;
        }

        public Connection AddConnection(DiagramPoint from, DiagramPoint to)
        {
            Connection con = new Connection(from, to);
            con.Site = this;
            this.AddConnection(con);
            return con;
        }

        public Connection AddConnection(Connection con)
        {
            connections.Add(con);
            con.Site = this;
            con.From.Site = this;
            con.To.Site = this;
            this.Invalidate(true);
            return con;
        }

        public Connection AddConnection(Point startPoint)
        {
            //let's take a random point and assume this control is not infinitesimal (bigger than 20x20).
            Point rndPoint = new Point(rnd.Next(20, this.Width - 20), rnd.Next(20, this.Height - 20));
            Connection con = new Connection(startPoint, rndPoint);
            con.Site = this;
            this.AddConnection(con);
            //use the end-point and simulate a dragging operation, see the OnMouseDown handler
            selectedEntity = con.To;
            tracking = true;
            this.Invalidate(true);
            return con;
        }

        public void DeleteSelectElement()
        {
            DeleteElement(selectedEntity);
        }

        private void OnDeleting(object sender, DeletingEventArgs e)
        {
            OnElementDeleting?.Invoke(sender, e);
        }

        private void OnDeleted(object sender, EventArgs e)
        {
            OnElementDeleted?.Invoke(sender, e);
        }

        private void OnSelectChanged(object sender, SelectElementChangedEventArgs e)
        {
            OnSelectElementChanged?.Invoke(sender, e);
        }

        private void UpdateSelected(Entity oEnt)
        {
            if (selectedEntity != null)
            {
                selectedEntity.IsSelected = false;
                selectedEntity.Invalidate();
                selectedEntity = null;
            }

            if (oEnt != null)
            {
                selectedEntity = oEnt;
                oEnt.IsSelected = true;
                oEnt.Invalidate();
            }
        }

        protected void UpdateHovered(Entity oEnt = null)
        {
            if (hoveredEntity != null)
            {
                hoveredEntity.hovered = false;
                hoveredEntity.Invalidate();
                hoveredEntity = null;
            }

            if (oEnt != null)
            {
                oEnt.hovered = true;
                hoveredEntity = oEnt;
                hoveredEntity.Invalidate();
            }
        }

        #endregion

    }
    public class DiagramPoint : Component
    {
        private Point _point = new Point();
        [Description("X偏移量"), Category("Layout")]
        public int X
        {
            get
            {
                return _point.X;
            }
            set
            {
                _point.X = value;
            }
        }

        [Description("Y偏移量"), Category("Layout")]
        public int Y
        {
            get
            {
                return _point.Y;
            }
            set
            {
                _point.Y = value;
            }
        }

        public ConnectorDirection ConnectorDirection = ConnectorDirection.None;

        public DiagramPoint()
        {

        }

        public DiagramPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public Point GetPoint()
        {
            return _point;
        }

        public override string ToString()
        {
            return $"{X},{Y}";
        }

        public static implicit operator DiagramPoint(Point point)
        {
            return new DiagramPoint(point.X, point.Y);
        }

        public DiagramPoint Copy()
        {
            DiagramPoint diagramPoint = new DiagramPoint(this.X, this.Y);
            diagramPoint.ConnectorDirection = this.ConnectorDirection;
            return diagramPoint;
        }
    }

    public enum ConnectorDirection
    {
        None,
        Up,
        Right,
        Left,
        Down
    }

    public class DeletingEventArgs : EventArgs
    {
        public bool Cancel { get; set; } = false;
    }

    public class SelectElementChangedEventArgs : EventArgs
    {
        public Entity CurrentEntity { get; set; }
        public Entity PreviousEntity { get; set; }
    }

    public class Proxy
    {
        #region Fields

        private DiagramControl site;

        #endregion

        #region Constructor

        public Proxy(DiagramControl site)
        { this.site = site; }

        #endregion

        #region Fields && Properties

        [Browsable(false)]
        public DiagramControl Site
        {
            get { return site; }
            set { site = value; }
        }
        [Browsable(true), Description("背景颜色"), Category("Layout")]
        public Color BackColor
        {
            get { return this.site.BackColor; }
            set { this.site.BackColor = value; }
        }

        [Browsable(true), Description("获取/设置网格显示状态"), Category("Layout")]
        public bool ShowGrid
        {
            get { return this.site.ShowGrid; }
            set { this.site.ShowGrid = value; }
        }

        [Browsable(true), Description("视觉原点，左上角坐标"), Category("Layout")]
        public DiagramPoint ViewOriginPoint
        {
            get { return this.site.ViewOriginPoint; }
            set { this.site.ViewOriginPoint = value; }
        }

        [Browsable(true), Description("连线默认颜色"), Category("Layout")]
        public Color LineColor
        {
            get { return this.site.LineColor; }
            set { this.site.LineColor = value; }
        }

        [Browsable(true), Description("连线选中颜色"), Category("Layout")]
        public Color LineSelectedColor
        {
            get { return this.site.LineSelectedColor; }
            set { this.site.LineSelectedColor = value; }
        }

        [Browsable(true), Description("连线悬停颜色"), Category("Layout")]
        public Color LineHoveredColor
        {
            get { return this.site.LineHoveredColor; }
            set { this.site.LineHoveredColor = value; }
        }

        #endregion
    }

    public class SaveDataInfo
    {
        public Point ViewOriginPoint { get; set; }

        public int LineHoveredColorARGB
        {
            get
            {
                return LineHoveredColor.ToArgb();
            }
            set
            {
                LineHoveredColor = Color.FromArgb(value);
            }
        }

        [XmlIgnore()]
        public Color LineHoveredColor { get; set; } = Color.Blue;

        public int LineSelectedColorARGB
        {
            get
            {
                return LineSelectedColor.ToArgb();
            }
            set
            {
                LineSelectedColor = Color.FromArgb(value);
            }
        }

        [XmlIgnore()]
        public Color LineSelectedColor { get; set; } = Color.Green;

        public int LineColorARGB
        {
            get
            {
                return LineColor.ToArgb();
            }
            set
            {
                LineColor = Color.FromArgb(value);
            }
        }

        [XmlIgnore()]
        public Color LineColor { get; set; } = Color.Silver;

        public int BackColorARGB
        {
            get
            {
                return BackColor.ToArgb();
            }
            set
            {
                BackColor = Color.FromArgb(value);
            }
        }

        [XmlIgnore()]
        public Color BackColor { get; set; } = SystemColors.Control;
        public bool ShowGrid { get; set; } = false;
        public List<SaveShape> Shapes { get; set; }

        public List<SaveConnection> Connections { get; set; }

        public static implicit operator SaveDataInfo(DiagramControl diagramControl)
        {
            SaveDataInfo saveDataInfo = new SaveDataInfo();
            saveDataInfo.ViewOriginPoint = diagramControl.ViewOriginPoint.GetPoint();
            saveDataInfo.LineHoveredColor = diagramControl.LineHoveredColor;
            saveDataInfo.LineSelectedColor = diagramControl.LineSelectedColor;
            saveDataInfo.LineColor = diagramControl.LineColor;
            saveDataInfo.BackColor = diagramControl.BackColor;
            saveDataInfo.ShowGrid = diagramControl.ShowGrid;
            saveDataInfo.Shapes = new List<SaveShape>();
            saveDataInfo.Connections = new List<SaveConnection>();
            foreach (ShapeBase ShapeBase in diagramControl.ShapeCollection)
            {
                saveDataInfo.Shapes.Add(ShapeBase);
            }

            foreach (Connection connection in diagramControl.Connections)
            {
                saveDataInfo.Connections.Add(connection);
            }

            return saveDataInfo;
        }
    }

    public class SaveConnection
    {
        public string ObjectID { get; set; }
        public string FromObjectID { get; set; }
        public string FromContainEntityObjectID { get; set; }
        public int FromContainEntityIndex { get; set; } = -1;
        public string ToObjectID { get; set; }
        public string ToContainEntityObjectID { get; set; }
        public int ToContainEntityIndex { get; set; } = -1;

        public static implicit operator SaveConnection(Connection connection)
        {
            SaveConnection saveConnection = new SaveConnection();
            saveConnection.ObjectID = connection.ObjectID;
            saveConnection.FromObjectID = connection.From.ObjectID;
            saveConnection.FromContainEntityObjectID = connection.From.ContainEntity.ObjectID;
            saveConnection.FromContainEntityIndex = connection.From.ConnectorsIndexOfContainEntity;
            saveConnection.ToObjectID = connection.To.ObjectID;
            saveConnection.ToContainEntityObjectID = connection.To.ContainEntity.ObjectID;
            saveConnection.ToContainEntityIndex = connection.To.ConnectorsIndexOfContainEntity;
            return saveConnection;
        }
    }

    public class SaveShape
    {
        public static implicit operator SaveShape(ShapeBase shape)
        {
            SaveShape saveShape = new SaveShape();
            saveShape.AssemblyInfo = shape.GetType().Assembly.FullName;
            saveShape.Type = shape.GetType().FullName;
            saveShape.ObjectID = shape.ObjectID;
            saveShape.X = shape.X;
            saveShape.Y = shape.Y;
            saveShape.Text = shape.Text;
            saveShape.BackGroundColor = shape.BackGroundColor;
            saveShape.BoderColor = shape.BoderColor;
            saveShape.BoderSelectedColor = shape.BoderSelectedColor;
            saveShape.EnableBottomSourceConnector = shape.EnableBottomSourceConnector;
            saveShape.EnableLeftSourceConnector = shape.EnableLeftSourceConnector;
            saveShape.EnableRightSourceConnector = shape.EnableRightSourceConnector;
            saveShape.EnableTopSourceConnector = shape.EnableTopSourceConnector;
            saveShape.EnableBottomTargetConnector = shape.EnableBottomTargetConnector;
            saveShape.EnableLeftTargetConnector = shape.EnableLeftTargetConnector;
            saveShape.EnableRightTargetConnector = shape.EnableRightTargetConnector;
            saveShape.EnableTopTargetConnector = shape.EnableTopTargetConnector;
            saveShape.ShowBorder = shape.ShowBorder;
            return saveShape;
        }
        public string AssemblyInfo { get; set; }
        public string Type { get; set; }
        public string ObjectID { get; set; }
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public string Text { get; set; }

        public int BackGroundColorARGB
        {
            get
            {
                return BackGroundColor.ToArgb();
            }
            set
            {
                BackGroundColor = Color.FromArgb(value);
            }
        }

        [XmlIgnore()]
        public Color BackGroundColor { get; set; } = Color.White;

        public int BoderColorARGB
        {
            get
            {
                return BoderColor.ToArgb();
            }
            set
            {
                BoderColor = Color.FromArgb(value);
            }
        }
        [XmlIgnore()]
        public Color BoderColor { get; set; } = Color.Black;

        public int BoderSelectedColorARGB
        {
            get
            {
                return BoderSelectedColor.ToArgb();
            }
            set
            {
                BoderSelectedColor = Color.FromArgb(value);
            }
        }

        [XmlIgnore()]
        public Color BoderSelectedColor { get; set; } = Color.GreenYellow;
        public bool EnableBottomSourceConnector { get; set; } = true;
        public bool EnableLeftSourceConnector { get; set; } = true;
        public bool EnableRightSourceConnector { get; set; } = true;
        public bool EnableTopSourceConnector { get; set; } = true;
        public bool EnableBottomTargetConnector { get; set; } = true;
        public bool EnableLeftTargetConnector { get; set; } = true;
        public bool EnableRightTargetConnector { get; set; } = true;
        public bool EnableTopTargetConnector { get; set; } = true;
        public bool ShowBorder { get; set; } = true;
    }
}
