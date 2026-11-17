using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoLoop {
    // Layout is written in 96-DPI pixels while fonts are in points, so pixel sizes must grow with the display scale
    // or text clips at 125-200%. Simulated lets --preview render other scales on a 100% display.
    internal static class UiScale {
        public static float Dpi=1f, Simulated=1f;
        public static float Layout { get { return Dpi*Simulated; } }
        public static void Init(){try{using(Graphics g=Graphics.FromHwnd(IntPtr.Zero))Dpi=Math.Max(1f,g.DpiX/96f);}catch{Dpi=1f;}}
        public static int Px(float value){return (int)Math.Round(value*Layout);}
        // Fonts are read before any are assigned, so inherited fonts are not scaled twice.
        public static void ScaleFonts(Control root,float factor){if(factor==1f)return;var all=new System.Collections.Generic.List<Control>();Action<Control> walk=null;walk=c=>{all.Add(c);foreach(Control child in c.Controls)walk(child);};walk(root);var fonts=new Font[all.Count];for(int i=0;i<all.Count;i++)fonts[i]=new Font(all[i].Font.FontFamily,all[i].Font.Size*factor,all[i].Font.Style);for(int i=0;i<all.Count;i++)all[i].Font=fonts[i];}
    }
    internal class ThemedDialog : Form {
        Panel titleRow;Label titleLabel;bool layoutScaled;
        protected override void OnLoad(EventArgs e){if(!layoutScaled){layoutScaled=true;float k=UiScale.Layout;if(k!=1f){Scale(new SizeF(k,k));UiScale.ScaleFonts(this,UiScale.Simulated);titleRow.Width=Math.Max(40,ClientSize.Width-UiScale.Px(24));}}base.OnLoad(e);}
        public ThemedDialog(){FormBorderStyle=FormBorderStyle.None;BackColor=Color.FromArgb(15,27,39);ForeColor=Color.FromArgb(225,234,244);Font=new Font("Segoe UI",10);Padding=new Padding(12,43,12,12);StartPosition=FormStartPosition.CenterParent;titleRow=new Panel{Location=new Point(12,7),Size=new Size(380,31),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};titleLabel=new Label{Text=Text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",11,FontStyle.Bold)};IconButton close=new IconButton{IconKind="close",Bare=true,BackColor=BackColor,ForeColor=ForeColor,Dock=DockStyle.Right,Width=28,AccessibleName="Close"};close.Click+=delegate{Close();};titleRow.Controls.Add(titleLabel);titleRow.Controls.Add(close);Controls.Add(titleRow);TextChanged+=delegate{titleLabel.Text=Text;};MouseEventHandler drag=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};titleLabel.MouseDown+=drag;titleRow.MouseDown+=drag;}
        protected override void OnResize(EventArgs e){base.OnResize(e);if(titleRow!=null)titleRow.Width=Math.Max(40,ClientSize.Width-UiScale.Px(24));if(Width>30&&Height>30)using(GraphicsPath p=UiDrawing.Round(new Rectangle(0,0,Width,Height),9)){Region old=Region;Region=new Region(p);if(old!=null)old.Dispose();}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(GraphicsPath p=UiDrawing.Round(new Rectangle(0,0,Width-1,Height-1),9))using(Pen border=new Pen(Color.FromArgb(57,73,89)))e.Graphics.DrawPath(border,p);}
    }
    internal static class UiDrawing {
        public static GraphicsPath Round(Rectangle r,int radius){GraphicsPath p=new GraphicsPath();int d=Math.Max(2,radius*2);p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        public static void Face(Graphics g,Rectangle r,Color fill,Color border,int radius){g.SmoothingMode=SmoothingMode.AntiAlias;using(GraphicsPath p=Round(r,radius)){using(Brush b=new SolidBrush(fill))g.FillPath(b,p);if(border!=Color.Empty)using(Pen pen=new Pen(border))g.DrawPath(pen,p);}}
    }
    internal class RoundedButton : Button {
        bool hover;
        public RoundedButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;DoubleBuffered=true;Cursor=Cursors.Hand;}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaintBackground(PaintEventArgs e){using(Brush background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);}
        protected override void OnPaint(PaintEventArgs e){using(Brush background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);Color face=hover?ControlPaint.Light(BackColor,0.06f):BackColor;UiDrawing.Face(e.Graphics,new Rectangle(0,0,Width-1,Height-1),face,Color.FromArgb(48,64,81),6);TextFormatFlags alignment=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;int inset=UiScale.Layout>1f?UiScale.Px(6):10;TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(inset,1,Width-2*inset,Height-2),Enabled?ForeColor:Color.FromArgb(120,135,150),alignment|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
    }
    internal class IconButton : RoundedButton {
        public string IconKind="settings";public bool Bare;
        protected override void OnPaint(PaintEventArgs e){Graphics g=e.Graphics;using(Brush background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))g.FillRectangle(background,ClientRectangle);g.SmoothingMode=SmoothingMode.AntiAlias;if(!Bare)UiDrawing.Face(g,new Rectangle(0,0,Width-1,Height-1),BackColor,Color.FromArgb(48,64,81),6);g.TranslateTransform(Width/2f,Height/2f);g.ScaleTransform(UiScale.Layout,UiScale.Layout);float cx=0,cy=0;Color color=Enabled?ForeColor:Color.Gray;using(Pen pen=new Pen(color,2)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;if(IconKind=="close"){g.DrawLine(pen,cx-5,cy-5,cx+5,cy+5);g.DrawLine(pen,cx+5,cy-5,cx-5,cy+5);}else if(IconKind=="minimize")g.DrawLine(pen,cx-6,cy,cx+6,cy);else if(IconKind=="refresh"){g.DrawArc(pen,cx-9,cy-9,18,18,215,145);g.DrawArc(pen,cx-9,cy-9,18,18,35,145);using(Brush b=new SolidBrush(color)){g.FillPolygon(b,new PointF[]{new PointF(cx+9,cy-9),new PointF(cx+9,cy),new PointF(cx+2,cy-4)});g.FillPolygon(b,new PointF[]{new PointF(cx-9,cy+9),new PointF(cx-9,cy),new PointF(cx-2,cy+4)});}}else{PointF[] points=new PointF[64];for(int i=0;i<64;i++){double a=i*Math.PI*2/64-Math.PI/2;double r=(i%8<2||i%8>5)?10:7.5;points[i]=new PointF(cx+(float)(Math.Cos(a)*r),cy+(float)(Math.Sin(a)*r));}using(Brush b=new SolidBrush(color))g.FillPolygon(b,points);using(Brush b=new SolidBrush(BackColor))g.FillEllipse(b,cx-4,cy-4,8,8);}}}
    }
    internal class LoopLogo : Control {
        public LoopLogo(){DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(Brush background=new SolidBrush(Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);e.Graphics.ScaleTransform(Width/40f,Height/34f);using(Pen p=new Pen(Color.FromArgb(103,241,173),5)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;using(GraphicsPath path=new GraphicsPath()){path.AddBezier(31,10,17,4,18,29,8,25);path.AddBezier(8,25,-4,20,6,4,15,13);path.AddBezier(15,13,23,19,23,30,32,25);path.AddBezier(32,25,39,21,38,15,35,12);e.Graphics.DrawPath(p,path);}}using(Brush b=new SolidBrush(Color.FromArgb(103,241,173)))e.Graphics.FillPolygon(b,new PointF[]{new PointF(29,3),new PointF(38,10),new PointF(28,15)});}
    }
    internal class StatusLabel : Label {
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color dot=Text.StartsWith("Paused")?Color.FromArgb(245,203,112):Color.FromArgb(68,220,143);int size=UiScale.Px(8),textLeft=UiScale.Px(17);using(Brush b=new SolidBrush(dot))e.Graphics.FillEllipse(b,1,Height/2-size/2,size,size);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(textLeft,0,Math.Max(0,Width-textLeft),Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
    }
    internal class MintCheckBox : CheckBox {
        public MintCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
        public override Size GetPreferredSize(Size proposed){Size text=TextRenderer.MeasureText(Text,Font);return new Size(text.Width+UiScale.Px(30),Math.Max(UiScale.Px(25),text.Height+4));}
        protected override void OnPaint(PaintEventArgs e){using(Brush background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);int box=UiScale.Px(18),top=(Height-box)/2;UiDrawing.Face(e.Graphics,new Rectangle(0,top,box,box),Checked?Color.FromArgb(103,241,173):Color.FromArgb(28,37,49),Checked?Color.FromArgb(103,241,173):Color.FromArgb(87,105,122),4);if(Checked)using(Pen p=new Pen(Color.FromArgb(19,40,39),2*UiScale.Layout)){p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;e.Graphics.DrawLines(p,new Point[]{new Point(UiScale.Px(4),top+UiScale.Px(9)),new Point(UiScale.Px(8),top+UiScale.Px(13)),new Point(UiScale.Px(14),top+UiScale.Px(5))});}int textLeft=UiScale.Px(29);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(textLeft,0,Width-textLeft,Height),Enabled?ForeColor:Color.Gray,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);}
    }
    internal class NumberBox : UserControl {
        readonly TextBox editor;decimal current,minimum,maximum=100;bool updating;public bool ShowBorder=true,ShowSpin=true;public int TextInset=9;
        public event EventHandler ValueChanged;
        public decimal Value{get{Commit();return current;}set{SetValue(value,true);}}
        public decimal Minimum{get{return minimum;}set{minimum=value;if(maximum<minimum)maximum=minimum;SetValue(Math.Max(minimum,Math.Min(maximum,current)),true);}}
        public decimal Maximum{get{return maximum;}set{maximum=value;if(minimum>maximum)minimum=maximum;SetValue(Math.Max(minimum,Math.Min(maximum,current)),true);}}
        public new BorderStyle BorderStyle{get{return ShowBorder?BorderStyle.FixedSingle:BorderStyle.None;}set{ShowBorder=value!=BorderStyle.None;Invalidate();}}
        public NumberBox(){DoubleBuffered=true;Height=35;BackColor=Color.FromArgb(24,36,50);ForeColor=Color.FromArgb(225,234,244);editor=new TextBox{BorderStyle=BorderStyle.None,BackColor=BackColor,ForeColor=ForeColor,Font=Font,Text="0",TabStop=true};Controls.Add(editor);editor.TextChanged+=delegate{if(updating)return;decimal parsed;if(decimal.TryParse(editor.Text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.CurrentCulture,out parsed)&&parsed>=minimum&&parsed<=maximum)SetValue(parsed,false);};editor.Leave+=delegate{Commit();};editor.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.A){editor.SelectAll();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down){Step(e.KeyCode==Keys.Up?1:-1);e.SuppressKeyPress=true;}};editor.KeyPress+=delegate(object sender,KeyPressEventArgs e){if(!char.IsControl(e.KeyChar)&&!char.IsDigit(e.KeyChar)&&!(e.KeyChar=='-'&&minimum<0))e.Handled=true;};Resize+=delegate{Arrange();};FontChanged+=delegate{if(editor!=null){editor.Font=Font;Arrange();}};BackColorChanged+=delegate{if(editor!=null)editor.BackColor=BackColor;};ForeColorChanged+=delegate{if(editor!=null)editor.ForeColor=ForeColor;};Arrange();}
        void SetValue(decimal value,bool rewrite){if(value<minimum||value>maximum)throw new ArgumentOutOfRangeException("value");value=decimal.Truncate(value);bool changed=current!=value;current=value;if(rewrite&&editor!=null){updating=true;editor.Text=current.ToString(System.Globalization.CultureInfo.CurrentCulture);updating=false;}if(changed&&ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}
        void Commit(){if(editor==null||updating)return;decimal parsed;if(!decimal.TryParse(editor.Text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.CurrentCulture,out parsed))parsed=current;parsed=Math.Max(minimum,Math.Min(maximum,parsed));SetValue(parsed,true);}
        void Step(int delta){Commit();Value=Math.Max(minimum,Math.Min(maximum,current+delta));}
        void Arrange(){if(editor==null)return;editor.Location=new Point(TextInset,Math.Max(3,(Height-editor.PreferredHeight)/2));editor.Width=Math.Max(20,Width-(ShowSpin?UiScale.Px(32):TextInset+8));Invalidate();}
        protected override void OnPaintBackground(PaintEventArgs e){using(Brush background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);UiDrawing.Face(e.Graphics,new Rectangle(0,0,Width-1,Height-1),BackColor,ShowBorder?Color.FromArgb(50,67,84):Color.Empty,6);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(!ShowSpin)return;float u=UiScale.Layout;e.Graphics.TranslateTransform(Width-UiScale.Px(13),Height/2);e.Graphics.ScaleTransform(u,u);using(Pen pen=new Pen(Enabled?Color.FromArgb(169,187,203):Color.Gray,1.4f)){e.Graphics.DrawLines(pen,new Point[]{new Point(-3,-5),new Point(0,-8),new Point(3,-5)});e.Graphics.DrawLines(pen,new Point[]{new Point(-3,5),new Point(0,8),new Point(3,5)});}e.Graphics.ResetTransform();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(ShowSpin&&e.X>Width-UiScale.Px(26))Step(e.Y<Height/2?1:-1);else editor.Focus();}
    }
}


