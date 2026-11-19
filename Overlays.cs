using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoLoop {
    // Small always-on-top windows shown while Auto Loop works. None of them ever takes keyboard focus or becomes
    // the active window, so they never pause playback or steal input from the app being automated.
    internal abstract class OverlayWindow : Form {
        protected OverlayWindow(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;DoubleBuffered=true;}
        protected override bool ShowWithoutActivation{get{return true;}}
        protected virtual bool ClickThrough{get{return false;}}
        protected override CreateParams CreateParams{get{CreateParams p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x8;if(ClickThrough)p.ExStyle|=0x20;return p;}}
        protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
        protected static int Px(float value){return UiScale.Px(value);}
        protected static Font UiFont(float points,FontStyle style){return new Font("Segoe UI",points*UiScale.Simulated,style);}
        // Paints onto a transparent bitmap with smooth edges. (Capturing the window instead would show its square,
        // unclipped corners, because the round window shape is only applied on screen.)
        public Bitmap Render(){
            CreateControl();Bitmap bitmap=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using(Graphics g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;PaintOverlay(g,true);}
            return bitmap;
        }
        protected abstract void PaintOverlay(Graphics g,bool image);
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;PaintOverlay(e.Graphics,false);}
        // ClearType text on screen; plain anti-aliased text on a transparent image, where ClearType leaves dark fringes.
        protected static void DrawLabel(Graphics g,bool image,string text,Font font,Rectangle bounds,Color color,bool center){
            if(image){using(StringFormat format=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})using(Brush brush=new SolidBrush(color))g.DrawString(text,font,brush,bounds,format);}
            else TextRenderer.DrawText(g,text,font,bounds,color,(center?TextFormatFlags.HorizontalCenter:TextFormatFlags.Left)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
    }

    // The pill at the top of the screen: what is happening, plus Pause and Stop. It can be dragged, and moves out of
    // the way when a playback click would land on it.
    internal class FloatingBar : OverlayWindow {
        public event EventHandler LeftClicked, RightClicked;
        string text="",left="",right="";Color dot=Color.FromArgb(68,220,143),rightEdge=Color.FromArgb(50,67,84);
        Rectangle leftRect,rightRect;bool atBottom;Screen screen;
        static readonly Color Background=Color.FromArgb(15,27,39),Border=Color.FromArgb(57,73,89),Ink=Color.FromArgb(225,234,244),Pill=Color.FromArgb(27,39,53),PillEdge=Color.FromArgb(50,67,84);
        public FloatingBar(){BackColor=Background;ForeColor=Ink;Font=UiFont(10,FontStyle.Regular);Text="Auto Loop status";Size=new Size(Px(200),Px(36));}
        public string Caption{get{return text;}}
        public void SetState(string text,Color dot,string left,string right,bool stopStyle){
            Color edge=stopStyle?Color.FromArgb(255,111,111):PillEdge;
            if(text==this.text&&left==this.left&&right==this.right&&dot==this.dot&&edge==rightEdge)return;
            this.text=text;this.dot=dot;this.left=left;this.right=right;rightEdge=edge;Relayout();Invalidate();
        }
        void Relayout(){
            int height=Px(36),x=Px(14)+Px(9)+Px(10);
            x+=TextRenderer.MeasureText(text,Font).Width+Px(10);
            leftRect=rightRect=Rectangle.Empty;
            if(left.Length>0){int w=TextRenderer.MeasureText(left,Font).Width+Px(16);leftRect=new Rectangle(x,Px(5),w,height-Px(10));x+=w+Px(6);}
            if(right.Length>0){int w=TextRenderer.MeasureText(right,Font).Width+Px(16);rightRect=new Rectangle(x,Px(5),w,height-Px(10));x+=w+Px(6);}
            x+=Px(4);int oldWidth=Width;Size=new Size(x,height);
            if(Visible)Left+=(oldWidth-x)/2;
            using(GraphicsPath path=UiDrawing.Round(new Rectangle(0,0,Width,Height),height/2)){Region old=Region;Region=new Region(path);if(old!=null)old.Dispose();}
        }
        public void PlaceNear(Point pointer){screen=Screen.FromPoint(pointer);atBottom=false;Place();}
        void Place(){if(screen==null)screen=Screen.FromControl(this);Rectangle area=screen.WorkingArea;Location=new Point(area.Left+(area.Width-Width)/2,atBottom?area.Bottom-Height-Px(12):area.Top+Px(12));}
        // Moves to the other edge of the screen (top or bottom) so a click can reach the window underneath.
        public void MoveAway(){atBottom=!atBottom;Place();}
        protected override void PaintOverlay(Graphics g,bool image){
            UiDrawing.Face(g,new Rectangle(0,0,Width-1,Height-1),Background,Border,(Height-1)/2);
            int size=Px(9),textLeft=Px(14)+size+Px(10);using(Brush b=new SolidBrush(dot))g.FillEllipse(b,Px(14),(Height-size)/2,size,size);
            int textRight=leftRect.IsEmpty?(rightRect.IsEmpty?Width-Px(8):rightRect.Left):leftRect.Left;
            DrawLabel(g,image,text,Font,new Rectangle(textLeft,0,Math.Max(0,textRight-textLeft),Height),Ink,false);
            DrawPill(g,image,leftRect,left,PillEdge);DrawPill(g,image,rightRect,right,rightEdge);
        }
        void DrawPill(Graphics g,bool image,Rectangle r,string caption,Color edge){if(r.IsEmpty)return;UiDrawing.Face(g,r,Pill,edge,Px(6));DrawLabel(g,image,caption,Font,r,Ink,true);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||leftRect.Contains(e.Location)||rightRect.Contains(e.Location))return;Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button!=MouseButtons.Left)return;if(leftRect.Contains(e.Location)&&LeftClicked!=null)LeftClicked(this,EventArgs.Empty);else if(rightRect.Contains(e.Location)&&RightClicked!=null)RightClicked(this,EventArgs.Empty);}
    }

    // The 3-2-1 countdown: a dark disc with a soft shadow and a mint-to-teal ring that drains every second, while the
    // number pops in. It is a per-pixel-alpha window, so its edges and shadow are smooth, and clicks pass through it.
    internal class CountdownRing : OverlayWindow {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct PT{public int X,Y;}
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct SZ{public int W,H;}
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential,Pack=1)] struct BLEND{public byte Op,Flags,Alpha,Format;}
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr hdcDst,ref PT pptDst,ref SZ psize,IntPtr hdcSrc,ref PT pptSrc,int crKey,ref BLEND pblend,int dwFlags);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr hdc);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        static readonly Color Disc=Color.FromArgb(232,11,17,24),Track=Color.FromArgb(22,255,255,255),Note=Color.FromArgb(166,182,198);
        readonly Timer animation=new Timer{Interval=15};long deadline;string note="";
        readonly Font numberFont=new Font("Segoe UI",Px(54),FontStyle.Regular,GraphicsUnit.Pixel),noteFont=new Font("Segoe UI",Px(11),FontStyle.Regular,GraphicsUnit.Pixel);
        readonly Brush shadowBrush=new SolidBrush(Color.FromArgb(9,0,0,0)),discBrush=new SolidBrush(Disc),noteBrush=new SolidBrush(Note);
        readonly Pen trackPen=new Pen(Track,Px(6));
        readonly StringFormat centered=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        public CountdownRing(){int size=Px(168);Size=new Size(size,size);Text="Auto Loop countdown";animation.Tick+=delegate{Redraw();};}
        protected override bool ClickThrough{get{return true;}}
        protected override CreateParams CreateParams{get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000;return p;}}
        public string Number{get{double left=Remaining;return left>0?Math.Ceiling(left).ToString():"";}}
        double Remaining{get{return Math.Max(0,(deadline-System.Diagnostics.Stopwatch.GetTimestamp())/(double)System.Diagnostics.Stopwatch.Frequency);}}
        // Shows the countdown centred on the screen under the pointer and animates it until the deadline.
        public void Run(long deadlineTimestamp,string caption,Point pointer){
            deadline=deadlineTimestamp;note=caption;
            Rectangle area=Screen.FromPoint(pointer).WorkingArea;Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);
            if(!Visible){Show();}Redraw();animation.Start();
        }
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible)animation.Stop();}
        protected override void Dispose(bool disposing){if(disposing){foreach(IDisposable d in new IDisposable[]{animation,numberFont,noteFont,shadowBrush,discBrush,noteBrush,trackPen,centered})d.Dispose();}base.Dispose(disposing);}
        void Redraw(){
            if(!IsHandleCreated||!Visible)return;
            double left=Remaining;if(left<=0){Hide();return;}
            double ceiling=Math.Ceiling(left),secondLeft=1-(ceiling-left);
            using(Bitmap frame=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
                using(Graphics g=Graphics.FromImage(frame)){g.Clear(Color.Transparent);DrawFrame(g,ceiling.ToString(),secondLeft,1-secondLeft,note);}
                Push(frame);
            }
        }
        void Push(Bitmap frame){
            IntPtr screen=GetDC(IntPtr.Zero),memory=CreateCompatibleDC(screen),bitmap=frame.GetHbitmap(Color.FromArgb(0)),old=SelectObject(memory,bitmap);
            try{PT dst=new PT{X=Left,Y=Top},src=new PT();SZ size=new SZ{W=frame.Width,H=frame.Height};BLEND blend=new BLEND{Op=0,Flags=0,Alpha=255,Format=1};UpdateLayeredWindow(Handle,screen,ref dst,ref size,memory,ref src,0,ref blend,2);}
            finally{SelectObject(memory,old);DeleteObject(bitmap);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
        }
        // ring: fraction of the ring still showing (1 = full); intoSecond: 0..1 time since the number changed.
        void DrawFrame(Graphics g,string number,double ring,double intoSecond,string caption){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float size=Width,disc=Px(132),x=(size-disc)/2,y=(size-disc)/2-Px(4);
            for(int i=8;i>=1;i--){float grow=Px(i*1.6f);g.FillEllipse(shadowBrush,x-grow,y-grow+Px(6),disc+2*grow,disc+2*grow);}
            g.FillEllipse(discBrush,x,y,disc,disc);
            float inset=Px(9),stroke=trackPen.Width;RectangleF track=new RectangleF(x+inset,y+inset,disc-2*inset,disc-2*inset);
            g.DrawEllipse(trackPen,track);
            float sweep=(float)(360*Math.Max(0,Math.Min(1,ring)));
            if(sweep>0.5f)using(LinearGradientBrush brush=new LinearGradientBrush(Rectangle.Round(RectangleF.Inflate(track,stroke,stroke)),UiDrawing.BrandStart,UiDrawing.BrandEnd,45f))using(Pen arc=new Pen(brush,stroke){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawArc(arc,track,-90,sweep);
            double pop=Math.Min(1,intoSecond/0.18);float scale=(float)(0.82+0.18*EaseOut(pop));int alpha=(int)(255*Math.Min(1,pop*1.2));
            GraphicsState state=g.Save();float cx=x+disc/2,cy=y+disc/2-Px(6);g.TranslateTransform(cx,cy);g.ScaleTransform(scale,scale);
            using(Brush ink=new SolidBrush(Color.FromArgb(alpha,255,255,255)))g.DrawString(number,numberFont,ink,0,0,centered);
            g.Restore(state);
            g.DrawString(caption,noteFont,noteBrush,new RectangleF(x,y+disc*0.66f,disc,Px(20)),centered);
        }
        static double EaseOut(double t){return 1-Math.Pow(1-t,3);}
        // Still image for previews and the README: "2" with the ring two-thirds full.
        protected override void PaintOverlay(Graphics g,bool image){DrawFrame(g,"2",0.66,1,note.Length>0?note:"F9 cancels");}
        public void SetPreviewNote(string caption){note=caption;}
    }

    // Round, click-through badge: the big 3-2-1 countdown, or the small dot that marks a picked click spot.
    internal class ScreenBadge : OverlayWindow {
        readonly bool dotStyle;string big="",small="";
        static readonly Color Mint=Color.FromArgb(103,241,173),Background=Color.FromArgb(15,27,39),Muted=Color.FromArgb(166,182,198);
        public ScreenBadge(bool dot){dotStyle=dot;BackColor=dot?Mint:Background;Opacity=dot?0.95:0.92;int size=dot?Px(18):Px(128);Size=new Size(size,size);Text="Auto Loop";
            using(GraphicsPath path=new GraphicsPath()){path.AddEllipse(0,0,size,size);Region=new Region(path);}}
        protected override bool ClickThrough{get{return true;}}
        public string Number{get{return big;}}
        public void SetText(string big,string small){if(big==this.big&&small==this.small)return;this.big=big;this.small=small;Invalidate();}
        public void ShowCentered(Point pointer){Rectangle area=Screen.FromPoint(pointer).WorkingArea;Location=new Point(area.Left+(area.Width-Width)/2,area.Top+(area.Height-Height)/2);if(!Visible)Show();}
        public void ShowAt(Point point){Location=new Point(point.X-Width/2,point.Y-Height/2);if(!Visible)Show();}
        protected override void PaintOverlay(Graphics g,bool image){
            using(Brush fill=new SolidBrush(BackColor))g.FillEllipse(fill,0,0,Width-1,Height-1);
            if(dotStyle){using(Pen ring=new Pen(Color.FromArgb(15,110,86),Px(3)))g.DrawEllipse(ring,Px(2),Px(2),Width-Px(4)-1,Height-Px(4)-1);return;}
            using(Pen ring=new Pen(Color.FromArgb(57,73,89),2))g.DrawEllipse(ring,1,1,Width-3,Height-3);
            using(Font number=UiFont(36,FontStyle.Regular))DrawLabel(g,image,big,number,new Rectangle(0,Px(14),Width,Px(70)),Mint,true);
            using(Font note=UiFont(8.5f,FontStyle.Regular))DrawLabel(g,image,small,note,new Rectangle(Px(8),Px(84),Width-Px(16),Px(22)),Muted,true);
        }
    }
}
