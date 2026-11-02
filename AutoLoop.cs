using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

[assembly: System.Reflection.AssemblyTitle("Auto Loop")]
[assembly: System.Reflection.AssemblyProduct("Auto Loop")]
[assembly: System.Reflection.AssemblyVersion("0.6.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.4.2.0")]
namespace AutoLoop {
    public class Step {
        public string Kind = "Wait";
        public int Code, X, Y, Delay;
        public bool Extended;
        public override string ToString() {
            string action = MainForm.StepLabel(this);


            if (Kind == "Move") action = "Move to " + X + ", " + Y;
            if (Kind == "Wheel") action = "Scroll " + Code;
            return Delay + " ms   |   " + action;
        }
    }
    public class Routine {
        public int Version = 1, Mode, Interval = 100, Hold = 30, Repeat = 10, LimitSeconds, Key = (int)Keys.F;
        public int MouseButton, FixedX, FixedY;
        public bool FixedPosition, AutoResume = true, LockTarget;
        public string TargetTitle = "";
        public List<Step> Steps = new List<Step>();
    }
    public class Preferences {
        public int[] Hotkeys = { (int)Keys.F6, (int)Keys.F7, (int)Keys.F8, (int)Keys.F9 };
    }
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct KBD { public uint vkCode, scanCode, flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] public struct MOUSE { public POINT pt; public uint data, flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] public struct MI { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] public struct KI { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Explicit)] public struct UNION { [FieldOffset(0)] public MI mi; [FieldOffset(0)] public KI ki; }
        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public UNION u; }
        public delegate IntPtr Hook(int code, IntPtr message, IntPtr data);
        public delegate bool EnumProc(IntPtr window, IntPtr param);
        [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint count, INPUT[] input, int size);
        [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int type, Hook hook, IntPtr module, uint thread);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet=CharSet.Auto)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr window, int id, uint mods, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint kind);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        public static string Title(IntPtr window) { StringBuilder s = new StringBuilder(512); GetWindowText(window,s,s.Capacity); return s.ToString(); }
        public static uint ProcessId(IntPtr window) { uint p; GetWindowThreadProcessId(window,out p); return p; }
        public static bool Extended(int key) { return (MapVirtualKey((uint)key,4) & 0xFF00) != 0; }
        public static INPUT Key(int key, bool up, bool extended) {
            INPUT i = new INPUT(); i.type=1; i.u.ki.vk=(ushort)key; i.u.ki.flags=(up?2u:0u)|(extended?1u:0u); return i;
        }
        public static INPUT Mouse(int button, bool up) {
            INPUT i=new INPUT(); uint flag=0;
            if (button==(int)MouseButtons.Left) flag=up?4u:2u;
            else if (button==(int)MouseButtons.Right) flag=up?16u:8u;
            else if (button==(int)MouseButtons.Middle) flag=up?64u:32u;
            else { flag=up?0x100u:0x80u; i.u.mi.mouseData=button==(int)MouseButtons.XButton1?1u:2u; }
            i.u.mi.flags=flag; return i;
        }
        public static INPUT Move(int x, int y) {
            Rectangle r=SystemInformation.VirtualScreen; INPUT i=new INPUT();
            i.u.mi.dx=(int)Math.Round((x-r.Left)*65535.0/Math.Max(1,r.Width-1));
            i.u.mi.dy=(int)Math.Round((y-r.Top)*65535.0/Math.Max(1,r.Height-1));
            i.u.mi.flags=0x8000|0x4000|1; return i;
        }
        public static void Send(INPUT i) { if(SendInput(1,new INPUT[]{i},Marshal.SizeOf(typeof(INPUT)))!=1) throw new InvalidOperationException("Windows blocked the input. The target may have higher permissions or may not support simulated input."); }
    }
    internal class WindowChoice {
        public IntPtr Handle; public uint Process; public string Title;
        public override string ToString() { return Title; }
    }
    internal class KeyChoice {
        public int Code;
        public override string ToString(){return MainForm.KeyName(Code);}
    }
    internal class ShortcutDialog : ThemedDialog {
        public int Binding; Label value;
        public ShortcutDialog(string title, int current) {
            Text=title; ClientSize=new Size(420,230); StartPosition=FormStartPosition.CenterParent;
            MaximizeBox=false; MinimizeBox=false; KeyPreview=true; Font=new Font("Segoe UI",10); Binding=current;
            Label hint=new Label{Text="Press your key, then choose Use.",Dock=DockStyle.Top,Height=40,Padding=new Padding(6)};
            value=new Label{Text=MainForm.KeyName(current),Dock=DockStyle.Top,Height=42,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",16,FontStyle.Bold)};
            FlowLayoutPanel row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45,FlowDirection=FlowDirection.RightToLeft};
            Button ok=new RoundedButton{Text="Use",DialogResult=DialogResult.OK,Width=90,Height=34,BackColor=Color.FromArgb(36,91,82),ForeColor=Color.FromArgb(103,241,173)}; Button cancel=new RoundedButton{Text="Cancel",DialogResult=DialogResult.Cancel,Width=90,Height=34,BackColor=Color.FromArgb(30,36,48),ForeColor=ForeColor};
            row.Controls.Add(ok); row.Controls.Add(cancel); Controls.Add(value); Controls.Add(hint); Controls.Add(row); CancelButton=cancel;
            KeyDown+=delegate(object s,KeyEventArgs e){ e.SuppressKeyPress=true; if(MainForm.IsModifier((int)e.KeyCode)) return; Binding=(int)e.KeyData; value.Text=MainForm.KeyName(Binding); };
        }
        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message message,Keys keyData){int key=(int)(keyData&Keys.KeyCode);if(!MainForm.IsModifier(key)){Binding=(int)keyData;value.Text=MainForm.KeyName(Binding);}return true;}
    }
    internal class RoundedPanel : Panel {
        public RoundedPanel(){DoubleBuffered=true;}
        protected override void OnPaintBackground(PaintEventArgs e){using(Brush background=new SolidBrush(Parent==null?Color.FromArgb(20,24,33):Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;Rectangle r=new Rectangle(0,0,Width-1,Height-1);using(var path=new System.Drawing.Drawing2D.GraphicsPath()){int d=14;path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();using(Brush fill=new SolidBrush(BackColor))e.Graphics.FillPath(fill,path);using(Pen border=new Pen(Color.FromArgb(36,49,63)))e.Graphics.DrawPath(border,path);}}
    }
    internal class DarkComboBox : ComboBox {
        public bool BorderVisible=true;
        public DarkComboBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){using(Brush background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);UiDrawing.Face(e.Graphics,new Rectangle(0,0,Width-1,Height-1),BackColor,BorderVisible?Color.FromArgb(50,67,84):Color.Empty,6);string caption=SelectedIndex<0?"Choose app":GetItemText(SelectedItem);TextRenderer.DrawText(e.Graphics,caption,Font,new Rectangle(9,0,Math.Max(0,Width-35),Height),Enabled?ForeColor:Color.FromArgb(120,135,150),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(Pen pen=new Pen(Enabled?Color.FromArgb(187,202,217):Color.FromArgb(95,105,118),1.5f)){int x=Width-13,y=Height/2;e.Graphics.DrawLines(pen,new Point[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});}}
    }
    internal class ActionButton : Button {
        public string Symbol=""; public Color SymbolColor=Color.Empty;
        public ActionButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;DoubleBuffered=true;Cursor=Cursors.Hand;}
        protected override void OnPaintBackground(PaintEventArgs e){using(Brush background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle bounds=new Rectangle(0,0,Width-1,Height-1);using(var shape=new System.Drawing.Drawing2D.GraphicsPath()){int r=10;shape.AddArc(bounds.Left,bounds.Top,r,r,180,90);shape.AddArc(bounds.Right-r,bounds.Top,r,r,270,90);shape.AddArc(bounds.Right-r,bounds.Bottom-r,r,r,0,90);shape.AddArc(bounds.Left,bounds.Bottom-r,r,r,90,90);shape.CloseFigure();using(Brush b=new SolidBrush(BackColor))e.Graphics.FillPath(b,shape);using(Pen p=new Pen(Focused?Color.FromArgb(103,241,173):Color.FromArgb(48,64,81)))e.Graphics.DrawPath(p,shape);}
            float actionScale=Height/118f;e.Graphics.ScaleTransform(actionScale,actionScale);float contentWidth=Width/actionScale;
            string[] parts=Text.Split('\u00B7');StringFormat center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
            float cx=contentWidth/2f,cy=33;using(Brush icon=new SolidBrush(SymbolColor.IsEmpty?ForeColor:SymbolColor)){if(parts[0].Trim()=="Start")e.Graphics.FillPolygon(icon,new PointF[]{new PointF(cx-11,cy-15),new PointF(cx+15,cy),new PointF(cx-11,cy+15)});else if(parts[0].Trim()=="Record")e.Graphics.FillEllipse(icon,cx-13,cy-13,26,26);else if(parts[0].Trim()=="Pause"){e.Graphics.FillRectangle(icon,cx-10,cy-13,7,26);e.Graphics.FillRectangle(icon,cx+3,cy-13,7,26);}else if(parts[0].Trim()=="Stop")e.Graphics.FillRectangle(icon,cx-12,cy-12,24,24);else{PointF[] teeth=new PointF[64];for(int i=0;i<64;i++){double angle=i*Math.PI*2/64;double radius=i%8<2||i%8>5?14:10;teeth[i]=new PointF(cx+(float)(Math.Cos(angle)*radius),cy+(float)(Math.Sin(angle)*radius));}e.Graphics.FillPolygon(icon,teeth);using(Brush hole=new SolidBrush(BackColor))e.Graphics.FillEllipse(hole,cx-6,cy-6,12,12);}}
            using(Font label=new Font("Segoe UI",14,FontStyle.Regular))using(Brush b=new SolidBrush(ForeColor))e.Graphics.DrawString(parts[0].Trim(),label,b,new RectangleF(0,62,contentWidth,26),center);
            using(Font shortcut=new Font("Segoe UI",12))using(Brush b=new SolidBrush(Color.FromArgb(166,185,202)))e.Graphics.DrawString(parts.Length>1?parts[1].Trim():"",shortcut,b,new RectangleF(0,91,contentWidth,22),center);center.Dispose();
        }
    }
    public class MainForm : Form {
        readonly Color bg=Color.FromArgb(15,27,39), card=Color.FromArgb(27,39,53), field=Color.FromArgb(24,36,50), ink=Color.FromArgb(225,234,244), muted=Color.FromArgb(166,182,198), accent=Color.FromArgb(103,241,173);
        ComboBox modes, mouse, targets, keyChoices; NumberBox interval, hold, repeats, limit, px, py; Button playButton, recordButton, pauseButton, stopButton, keyCapture;
        CheckBox fixedPosition, lockTarget, autoResume; Label status, detail, recordCount; ListBox events; Button[] shortcuts=new Button[4];
        List<Step> recorded=new List<Step>(), playback=new List<Step>();
        readonly Dictionary<int,bool> heldKeys=new Dictionary<int,bool>(); readonly HashSet<int> heldMouse=new HashSet<int>();
        readonly HashSet<int> recordKeys=new HashSet<int>(), recordMouse=new HashSet<int>();
        Preferences preferences=new Preferences(); System.Windows.Forms.Timer timer; Stopwatch wall=new Stopwatch();
        long lastWall, activeClock, due, recordedAt, lastMove=-100, completed, maxDuration; int index, repeatCount, actionKey=(int)Keys.F;
        bool pendingSingleRun; string focusPauseReason=""; bool running, recording, userPaused, focusPaused, wasEligible, busy, changingShortcuts, pendingPlay, pendingRecord;
        long countdownUntil; WindowChoice boundTarget; IntPtr keyboardHook, mouseHook; Native.Hook keyboardProc, mouseProc;
        string dataRoot; public bool SelfTestMode; public string LastMessage="Ready";
        internal bool? TestFocus; internal long? TestTime; internal Action<Native.INPUT> InputSink;
        public static string KeyName(int key) { return new KeysConverter().ConvertToString((Keys)key); }
        public static bool IsModifier(int k) { return k==16||k==17||k==18||k==91||k==92||k==160||k==161||k==162||k==163||k==164||k==165; }
        protected override void OnResize(EventArgs e){base.OnResize(e);if(FormBorderStyle==FormBorderStyle.None&&Width>30&&Height>30)using(var path=UiDrawing.Round(new Rectangle(0,0,Width,Height),10)){Region old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(FormBorderStyle==FormBorderStyle.None)using(var path=UiDrawing.Round(new Rectangle(0,0,Width-1,Height-1),10))using(Pen border=new Pen(Color.FromArgb(57,73,89)))e.Graphics.DrawPath(border,path);}
        public MainForm() {
            Text="Auto Loop"; StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",16); BackColor=bg; ForeColor=ink; AutoScaleMode=AutoScaleMode.None;
            dataRoot=AppDomain.CurrentDomain.BaseDirectory;
            BuildUi(); LoadPreferences(); RefreshWindows(); UpdateShortcuts();
            timer=new System.Windows.Forms.Timer{Interval=10}; timer.Tick+=delegate{TickEngine();};
            Shown+=delegate { if(SelfTestMode)return;InstallHooks(); if(!RegisterBindings(preferences.Hotkeys)){int[] fallback={(int)(Keys.Control|Keys.Alt|Keys.F6),(int)(Keys.Control|Keys.Alt|Keys.F7),(int)(Keys.Control|Keys.Alt|Keys.F8),(int)(Keys.Control|Keys.Alt|Keys.F9)};if(RegisterBindings(fallback)){preferences.Hotkeys=fallback;UpdateShortcuts();SetStatus("Ready","Some function keys were in use. Shortcuts now use Ctrl + Alt; you can change them below.");}} timer.Start(); };
            FormClosing+=delegate { timer.Stop(); StopAll("Stopped"); SavePreferences(); RemoveHooks(); for(int i=0;i<4;i++) Native.UnregisterHotKey(Handle,100+i); };
        }
        Label LabelFor(string text) { return new Label{Text=text,AutoSize=true,ForeColor=muted,Margin=new Padding(0,8,14,4)}; }
        Button ButtonFor(string text, int width) { return new RoundedButton{Text=text,Width=width,Height=36,FlatStyle=FlatStyle.Flat,BackColor=card,ForeColor=ink,Margin=new Padding(0,3,8,3),UseVisualStyleBackColor=false}; }
        NumberBox Number(int min,int max,int value,int width) { return new NumberBox{Minimum=min,Maximum=max,Value=value,Width=width,BackColor=field,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,5,12,5)}; }
        FlowLayoutPanel Row() { return new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=new Padding(0,3,0,3)}; }
        bool compactScaled; Button[] modeTabs=new Button[3]; TableLayoutPanel compactRoot, optionGrid, recordingTools; Panel recordingHost; Form settingsWindow;
        ToolTip tips=new ToolTip(); System.Windows.Forms.Timer positionCaptureTimer;
        ComboBox StyledCombo(int width) {
            ComboBox box=new DarkComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=width,Height=54,BackColor=field,ForeColor=ink,FlatStyle=FlatStyle.Flat,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=46};
            box.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();string text=e.Index<0?"Choose app":box.Items[e.Index].ToString();using(Brush brush=new SolidBrush(e.Index<0?muted:ink))e.Graphics.DrawString(text,box.Font,brush,e.Bounds.X+7,e.Bounds.Y+3);e.DrawFocusRectangle();};return box;
        }
        Label FieldLabel(string text){return new Label{Text=text,AutoSize=false,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=ink,Margin=new Padding(0)};}
        void SelectActionKey(int code){if(keyCapture!=null)keyCapture.Text=KeyName(code);for(int i=0;i<keyChoices.Items.Count;i++){if(((KeyChoice)keyChoices.Items[i]).Code==code){keyChoices.SelectedIndex=i;return;}}keyChoices.Items.Add(new KeyChoice{Code=code});keyChoices.SelectedIndex=keyChoices.Items.Count-1;}
        ComboBox stopRule; NumberBox stopValue; bool syncingStop;
        void SyncStopValues(){if(syncingStop)return;syncingStop=true;try{if(stopRule.SelectedIndex==0){repeats.Value=stopValue.Value;limit.Value=0;}else if(stopRule.SelectedIndex==1){repeats.Value=0;limit.Value=stopValue.Value;}else if(stopRule.SelectedIndex==2){repeats.Value=0;limit.Value=0;}}finally{syncingStop=false;}}
        void SetStopMode(){if(syncingStop)return;syncingStop=true;try{if(stopRule.SelectedIndex<3)while(stopRule.Items.Count>3)stopRule.Items.RemoveAt(3);stopValue.Maximum=stopRule.SelectedIndex==1?86400:1000000;stopValue.Visible=stopRule.SelectedIndex<2;if(stopRule.SelectedIndex==1)stopValue.Value=Math.Max(1,limit.Value);else if(stopRule.SelectedIndex==0)stopValue.Value=Math.Max(1,repeats.Value);}finally{syncingStop=false;}UpdateStopLayout();SyncStopValues();}
        void SyncStopDisplay(){syncingStop=true;try{while(stopRule.Items.Count>3)stopRule.Items.RemoveAt(3);if(repeats.Value>0&&limit.Value>0){stopRule.Items.Add(repeats.Value+" repeats or "+limit.Value+" seconds");stopRule.SelectedIndex=3;stopValue.Visible=false;}else if(limit.Value>0){stopRule.SelectedIndex=1;stopValue.Maximum=86400;stopValue.Value=limit.Value;stopValue.Visible=true;}else if(repeats.Value>0){stopRule.SelectedIndex=0;stopValue.Maximum=1000000;stopValue.Value=repeats.Value;stopValue.Visible=true;}else{stopRule.SelectedIndex=2;stopValue.Visible=false;}}finally{syncingStop=false;}UpdateStopLayout();}
        void UpdateStopLayout(){TableLayoutPanel layout=stopValue.Parent as TableLayoutPanel;if(layout!=null&&layout.ColumnStyles.Count>0)layout.ColumnStyles[0].Width=stopRule.SelectedIndex<2?65:0;}
        void SetEditingEnabled(bool enabled){foreach(Control control in new Control[]{mouse,keyCapture,interval,stopRule,stopValue,lockTarget,autoResume})control.Enabled=enabled;targets.Enabled=enabled&&lockTarget.Checked;foreach(Button tab in modeTabs)tab.Enabled=enabled;}
        void BuildUi() {
            MinimumSize=Size.Empty;MaximumSize=Size.Empty;FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;ClientSize=new Size(540,692);
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            compactRoot=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16,10,16,10),ColumnCount=1,RowCount=6};int[] heights={76,80,274,88,124,30};foreach(int height in heights)compactRoot.RowStyles.Add(new RowStyle(SizeType.Absolute,height));Controls.Add(compactRoot);
            Panel header=new Panel{Dock=DockStyle.Fill};LoopLogo logo=new LoopLogo{Location=new Point(0,2),Size=new Size(76,58)};header.Controls.Add(logo);header.Controls.Add(new Label{Text="Auto Loop",AutoSize=true,Font=new Font("Segoe UI",24,FontStyle.Bold),Location=new Point(100,10)});Button minimize=new IconButton{IconKind="minimize",Bare=true,BackColor=bg,ForeColor=muted,Size=new Size(31,32),Location=new Point(438,0),AccessibleName="Minimize"};minimize.Click+=delegate{WindowState=FormWindowState.Minimized;};Button close=new IconButton{IconKind="close",Bare=true,BackColor=bg,ForeColor=ink,Size=new Size(31,32),Location=new Point(470,0),AccessibleName="Close"};close.Click+=delegate{Close();};header.Controls.Add(minimize);header.Controls.Add(close);MouseEventHandler drag=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){Native.ReleaseCapture();Native.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};header.MouseDown+=drag;foreach(Control headerControl in header.Controls)if(!(headerControl is Button))headerControl.MouseDown+=drag;compactRoot.Controls.Add(header,0,0);
            modes=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};modes.Items.AddRange(new object[]{"Click","Keys","Record & replay"});modes.SelectedIndex=0;
            TableLayoutPanel tabs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Margin=new Padding(0,0,0,12)};string[] tabNames={"Click","Keys","Record"};for(int i=0;i<3;i++){int n=i;tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));modeTabs[i]=ButtonFor(tabNames[i],140);modeTabs[i].Dock=DockStyle.Fill;modeTabs[i].Margin=new Padding(0,0,i==2?0:4,0);modeTabs[i].Click+=delegate{if(!IsBusy())modes.SelectedIndex=n;};tabs.Controls.Add(modeTabs[i],i,0);}compactRoot.Controls.Add(tabs,0,1);
            RoundedPanel configuration=new RoundedPanel{Dock=DockStyle.Fill,BackColor=card,Padding=new Padding(14,10,14,10),Margin=new Padding(0,0,0,8)};compactRoot.Controls.Add(configuration,0,2);
            optionGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0)};optionGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));optionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,62));configuration.Controls.Add(optionGrid);
            Panel normalHost=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0),Size=new Size(480,264)};Label actionLabel=new Label{Text="Mouse button",AutoSize=true,ForeColor=ink,Location=new Point(0,28)};normalHost.Controls.Add(actionLabel);
            mouse=StyledCombo(380);mouse.Items.AddRange(new object[]{"Left click","Right click","Middle click"});mouse.SelectedIndex=0;mouse.Location=new Point(174,17);mouse.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;mouse.Width=306;normalHost.Controls.Add(mouse);
            keyChoices=StyledCombo(380);keyChoices.Location=mouse.Location;keyChoices.Size=mouse.Size;keyChoices.Anchor=mouse.Anchor;keyChoices.Visible=false;List<int> choiceCodes=new List<int>();for(int key=65;key<=90;key++)choiceCodes.Add(key);for(int key=48;key<=57;key++)choiceCodes.Add(key);for(int key=112;key<=135;key++)choiceCodes.Add(key);for(int key=96;key<=105;key++)choiceCodes.Add(key);choiceCodes.AddRange(new int[]{32,13,9,8,27,37,38,39,40,45,46,36,35,33,34,106,107,109,110,111});foreach(int code in choiceCodes)keyChoices.Items.Add(new KeyChoice{Code=code});SelectActionKey(actionKey);keyChoices.SelectedIndexChanged+=delegate{if(keyChoices.SelectedItem is KeyChoice)actionKey=((KeyChoice)keyChoices.SelectedItem).Code;};keyCapture=new RoundedButton{Text=KeyName(actionKey),BackColor=field,ForeColor=ink,Location=keyChoices.Location,Size=keyChoices.Size,Anchor=keyChoices.Anchor,Visible=false,TextAlign=ContentAlignment.MiddleLeft,AccessibleName="Choose repeated key"};tips.SetToolTip(keyCapture,"Click here, then press the key you want to repeat.");keyCapture.Click+=delegate{if(IsBusy())return;using(ModalScope scope=new ModalScope(this))using(ShortcutDialog dialog=new ShortcutDialog("Choose repeated key",actionKey)){if(dialog.ShowDialog(ModalOwner)==DialogResult.OK){if((dialog.Binding&(int)Keys.Modifiers)!=0){Message("Choose a single key. Use Record for combinations.");return;}actionKey=dialog.Binding;SelectActionKey(actionKey);keyCapture.Text=KeyName(actionKey);}}};normalHost.Controls.Add(keyCapture);
            normalHost.Controls.Add(new Label{Text="Interval",AutoSize=true,ForeColor=ink,Location=new Point(0,112)});interval=Number(10,3600000,100,210);interval.Font=new Font("Segoe UI",16);interval.Location=new Point(174,101);interval.Width=210;interval.Height=54;normalHost.Controls.Add(interval);normalHost.Controls.Add(new Label{Text="ms",AutoSize=true,ForeColor=muted,Location=new Point(400,112)});tips.SetToolTip(interval,"100 ms = one press every tenth of a second.");
            recordingHost=new Panel{Dock=DockStyle.Fill,Visible=false,Margin=new Padding(0)};recordCount=new Label{Text="Recorded routine · 0 steps",Dock=DockStyle.Top,Height=0,ForeColor=ink,Font=new Font("Segoe UI",10)};events=new ListBox{Dock=DockStyle.Fill,BackColor=field,ForeColor=ink,BorderStyle=BorderStyle.None,IntegralHeight=false,Font=new Font("Segoe UI",14),DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=45};events.DrawItem+=DrawRecordedStep;
            recordingTools=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=54,ColumnCount=4,RowCount=1,Padding=new Padding(0,6,0,2)};recordingTools.RowStyles.Add(new RowStyle(SizeType.Percent,100));for(int i=0;i<4;i++)recordingTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,i==0?32f:22.666f));Button delay=ButtonFor("Delay",75);delay.Click+=delegate{EditDelay();};Button remove=ButtonFor("Remove",75);remove.Click+=delegate{if(IsBusy()||events.SelectedIndex<0)return;recorded.RemoveAt(events.SelectedIndex);UpdateEvents();};Button clear=ButtonFor("Clear",75);clear.Click+=delegate{if(IsBusy())return;recorded.Clear();UpdateEvents();};Button save=ButtonFor("Save",75);save.Click+=delegate{SaveRoutine();};Button open=ButtonFor("Open",75);open.Click+=delegate{LoadRoutine();};ContextMenuStrip editMenu=new ContextMenuStrip();editMenu.Items.Add("Change delay",null,delegate{EditDelay();});editMenu.Items.Add("Remove selected step",null,delegate{if(!IsBusy()&&events.SelectedIndex>=0){recorded.RemoveAt(events.SelectedIndex);UpdateEvents();}});events.ContextMenuStrip=editMenu;Button testOnce=ButtonFor("Test once",100);testOnce.Click+=delegate{StartCountdown(false,true);};tips.SetToolTip(testOnce,"Play this recording once without changing its repeat or time settings.");Button[] tools={testOnce,clear,save,open};for(int i=0;i<tools.Length;i++){tools[i].Dock=DockStyle.Fill;tools[i].Margin=new Padding(0,0,i==3?0:6,0);tools[i].Font=new Font("Segoe UI",14);recordingTools.Controls.Add(tools[i],i,0);}recordingHost.Controls.Add(events);recordingHost.Controls.Add(recordCount);recordingHost.Controls.Add(recordingTools);
            Panel modeHost=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};modeHost.Controls.Add(normalHost);modeHost.Controls.Add(recordingHost);optionGrid.Controls.Add(modeHost,0,0);
            repeats=Number(0,1000000,10,100);limit=Number(0,86400,0,100);TableLayoutPanel stopRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0),Padding=new Padding(0,0,0,8)};stopRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));stopRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,174));stopRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));stopRow.Controls.Add(FieldLabel("Stop after"),0,0);stopValue=Number(1,1000000,10,78);stopValue.Dock=DockStyle.Top;stopValue.Margin=new Padding(0,3,7,0);stopRule=StyledCombo(200);stopRule.Dock=DockStyle.Top;stopRule.Items.AddRange(new object[]{"repeats","seconds","Until stopped"});stopRule.SelectedIndex=0;stopRule.Margin=new Padding(0);stopValue.ShowBorder=false;stopValue.ShowSpin=false;stopValue.TextInset=2;stopValue.BackColor=field;stopValue.Margin=new Padding(0);stopValue.Dock=DockStyle.Fill;((DarkComboBox)stopRule).BorderVisible=false;stopRule.BackColor=field;stopRule.Margin=new Padding(0);RoundedPanel unitGroup=new RoundedPanel{Dock=DockStyle.Fill,BackColor=field,Margin=new Padding(0),Padding=new Padding(7,4,7,4)};TableLayoutPanel unitLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};unitLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));unitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));unitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));unitLayout.Controls.Add(stopValue,0,0);unitLayout.Controls.Add(stopRule,1,0);unitGroup.Controls.Add(unitLayout);stopRow.Controls.Add(unitGroup,1,0);stopRule.SelectedIndexChanged+=delegate{SetStopMode();};stopValue.ValueChanged+=delegate{SyncStopValues();};optionGrid.Controls.Add(stopRow,0,1);
            Panel targetPanel=new Panel{Dock=DockStyle.Bottom,BackColor=card,Height=88,Padding=new Padding(0),Margin=new Padding(0)};configuration.Controls.Add(targetPanel);compactRoot.SetRowSpan(configuration,2);targetPanel.Paint+=delegate(object sender,PaintEventArgs e){using(Pen line=new Pen(Color.FromArgb(50,67,84)))e.Graphics.DrawLine(line,0,4,targetPanel.Width,4);};
            targetPanel.Controls.Add(new Label{Text="Run in",AutoSize=true,Location=new Point(0,31),ForeColor=ink});
            Button anyApp=ButtonFor("Any app",173),selectedApp=ButtonFor("Selected app",163);anyApp.Location=new Point(125,19);selectedApp.Location=new Point(302,19);anyApp.Height=selectedApp.Height=50;targetPanel.Controls.Add(anyApp);targetPanel.Controls.Add(selectedApp);
            Panel targetDetails=new Panel{Location=new Point(0,80),Size=new Size(480,100),Visible=false};targetPanel.Controls.Add(targetDetails);
            targets=StyledCombo(420);targets.Location=new Point(0,0);targetDetails.Controls.Add(targets);Button refresh=new IconButton{IconKind="refresh",Bare=true,BackColor=card,ForeColor=ink,Location=new Point(426,0),Size=new Size(32,32),AccessibleName="Refresh apps"};refresh.Click+=delegate{if(!IsBusy())RefreshWindows();};targetDetails.Controls.Add(refresh);
            lockTarget=new MintCheckBox{Checked=false,Visible=false};targetPanel.Controls.Add(lockTarget);autoResume=new MintCheckBox{Text="Resume when I return",Checked=true,AutoSize=true,ForeColor=ink,Location=new Point(0,58)};targetDetails.Controls.Add(autoResume);
            Action syncTarget=delegate{bool selected=lockTarget.Checked;targetDetails.Visible=selected;targets.Enabled=selected;anyApp.BackColor=selected?field:accent;anyApp.ForeColor=selected?ink:bg;selectedApp.BackColor=selected?accent:field;selectedApp.ForeColor=selected?bg:ink;targetPanel.Height=(int)Math.Round((selected?190:88)*(compactScaled?0.74f:1f));compactRoot.RowStyles[3].Height=(selected?190:88)*(compactScaled?0.74f:1f);ClientSize=compactScaled?new Size(400,selected?587:512):new Size(540,selected?794:692);};lockTarget.CheckedChanged+=delegate{syncTarget();};anyApp.Click+=delegate{if(!IsBusy())lockTarget.Checked=false;};selectedApp.Click+=delegate{if(!IsBusy())lockTarget.Checked=true;};syncTarget();
            TableLayoutPanel actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5,Margin=new Padding(0,2,0,4)};for(int i=0;i<5;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));playButton=new ActionButton{Text="Start · F6",Symbol="\u25B6",SymbolColor=accent,BackColor=card,ForeColor=ink};recordButton=new ActionButton{Text="Record · F7",Symbol="\u25CF",SymbolColor=Color.FromArgb(255,111,111),BackColor=card,ForeColor=ink};pauseButton=new ActionButton{Text="Pause · F8",Symbol="\u23F8",SymbolColor=ink,BackColor=card,ForeColor=ink};stopButton=new ActionButton{Text="Stop · F9",Symbol="\u25A0",SymbolColor=ink,BackColor=card,ForeColor=ink};Button gear=new ActionButton{Text="Settings",Symbol="\u2699",SymbolColor=ink,BackColor=card,ForeColor=ink};gear.AccessibleName="Settings";tips.SetToolTip(gear,"Shortcuts and advanced settings");gear.Click+=delegate{if(!IsBusy()){using(ModalScope scope=new ModalScope(this))settingsWindow.ShowDialog(ModalOwner);}};Button[] buttons={playButton,recordButton,pauseButton,stopButton,gear};for(int i=0;i<5;i++){buttons[i].Dock=DockStyle.Fill;buttons[i].Margin=new Padding(0,0,5,0);actions.Controls.Add(buttons[i],i,0);}playButton.Click+=delegate{StartCountdown(false);};recordButton.Click+=delegate{ToggleRecord();};pauseButton.Click+=delegate{TogglePause();};stopButton.Click+=delegate{StopAll("Stopped");};actions.Paint+=delegate(object sender,PaintEventArgs e){using(Pen separator=new Pen(Color.FromArgb(57,74,91))){int separatorX=gear.Left-3;e.Graphics.DrawLine(separator,separatorX,12,separatorX,actions.Height-12);}};compactRoot.Controls.Add(actions,0,4);
            Panel footer=new Panel{Dock=DockStyle.Fill};status=new StatusLabel{Text="Ready",Dock=DockStyle.Fill,ForeColor=ink,Font=new Font("Segoe UI",14,FontStyle.Regular),AutoEllipsis=true};detail=new Label{Text="Choose an app, then start.",Visible=false};tips.SetToolTip(status,detail.Text);footer.Controls.Add(status);compactRoot.Controls.Add(footer,0,5);BuildSettings();
            modes.SelectedIndexChanged+=delegate{int mode=modes.SelectedIndex;mouse.Visible=mode==0;keyCapture.Visible=mode==1;normalHost.Visible=mode!=2;recordingHost.Visible=mode==2;actionLabel.Text=mode==1?"Key to repeat":"Mouse button";if(mode==2)recordingHost.BringToFront();else normalHost.BringToFront();for(int i=0;i<3;i++){modeTabs[i].ForeColor=i==mode?bg:ink;modeTabs[i].BackColor=i==mode?accent:card;modeTabs[i].FlatAppearance.BorderColor=i==mode?accent:Color.FromArgb(57,69,84);}};modeTabs[0].ForeColor=bg;modeTabs[0].BackColor=accent;Scale(new SizeF(0.74f,0.74f));ScaleCompactFonts(this);keyCapture.Size=mouse.Size;compactScaled=true;ClientSize=new Size(400,512);interval.TextInset=7;interval.Height++;interval.Height--;
        }

        void ScaleCompactFonts(Control root){List<Control> all=new List<Control>();Action<Control> walk=null;walk=delegate(Control c){all.Add(c);foreach(Control child in c.Controls)walk(child);};walk(root);Font[] fonts=all.Select(c=>new Font(c.Font.FontFamily,c.Font.Size*0.74f,c.Font.Style)).ToArray();for(int i=0;i<all.Count;i++){all[i].Font=fonts[i];DarkComboBox combo=all[i] as DarkComboBox;if(combo!=null)combo.ItemHeight=(int)(combo.ItemHeight*0.74f);}keyCapture.Size=mouse.Size;events.ItemHeight=24;recordCount.Visible=false;}
        void BuildSettings() {
            settingsWindow=new ThemedDialog{Text="Auto Loop · Settings",ClientSize=new Size(490,450),Font=new Font("Segoe UI",12),BackColor=bg,ForeColor=ink,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.None,MaximizeBox=false,MinimizeBox=false};
            FlowLayoutPanel page=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};settingsWindow.Controls.Add(page);
            page.Controls.Add(new Label{Text="Shortcuts",Font=new Font("Segoe UI",13,FontStyle.Bold),AutoSize=true,ForeColor=accent});
            FlowLayoutPanel keyRow=new FlowLayoutPanel{Width=446,Height=88,WrapContents=true};string[] names={"Start","Record","Pause","Stop"};for(int i=0;i<4;i++){int n=i;shortcuts[i]=ButtonFor(names[i],212);shortcuts[i].Click+=delegate{ChangeShortcut(n);};keyRow.Controls.Add(shortcuts[i]);}page.Controls.Add(keyRow);
            page.Controls.Add(new Label{Text="Timing",Font=new Font("Segoe UI",12,FontStyle.Bold),AutoSize=true,ForeColor=accent,Margin=new Padding(0,12,0,5)});
            FlowLayoutPanel timing=Row();timing.Dock=DockStyle.None;timing.Width=446;timing.Height=42;hold=Number(1,60000,30,85);timing.Controls.Add(LabelFor("Press duration (ms)"));timing.Controls.Add(hold);page.Controls.Add(timing);
            page.Controls.Add(new Label{Text="Click position",Font=new Font("Segoe UI",12,FontStyle.Bold),AutoSize=true,ForeColor=accent,Margin=new Padding(0,12,0,5)});
            fixedPosition=new MintCheckBox{Text="Always click at the same screen position",AutoSize=true};page.Controls.Add(fixedPosition);
            FlowLayoutPanel position=Row();position.Dock=DockStyle.None;position.Width=446;position.Height=42;px=Number(-100000,100000,0,85);py=Number(-100000,100000,0,85);Button pick=ButtonFor("Pick in 3s",110);pick.Click+=delegate{settingsWindow.Hide();SetStatus("Position capture in 3 seconds","Move your pointer to the desired spot.");CancelPositionCapture();positionCaptureTimer=new System.Windows.Forms.Timer{Interval=3000};positionCaptureTimer.Tick+=delegate{CancelPositionCapture();px.Value=Math.Max(px.Minimum,Math.Min(px.Maximum,Cursor.Position.X));py.Value=Math.Max(py.Minimum,Math.Min(py.Maximum,Cursor.Position.Y));fixedPosition.Checked=true;SetStatus("Position captured","X: "+px.Value+"  Y: "+py.Value);};positionCaptureTimer.Start();};position.Controls.Add(LabelFor("X"));position.Controls.Add(px);position.Controls.Add(LabelFor("Y"));position.Controls.Add(py);position.Controls.Add(pick);page.Controls.Add(position);
            FlowLayoutPanel files=Row();files.Dock=DockStyle.None;files.Width=446;files.Height=44;Button save=ButtonFor("Save routine",140);save.Click+=delegate{SaveRoutine();};Button open=ButtonFor("Open routine",140);open.Click+=delegate{LoadRoutine();};Button done=ButtonFor("Done",100);done.DialogResult=DialogResult.OK;files.Controls.Add(save);files.Controls.Add(open);files.Controls.Add(done);page.Controls.Add(files);settingsWindow.AcceptButton=done;settingsWindow.CancelButton=done;
            settingsWindow.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;settingsWindow.Hide();}};
        }

        Form ModalOwner { get { return settingsWindow!=null&&settingsWindow.Visible?settingsWindow:this; } }
        bool IsBusy() { if(running||recording||pendingPlay||pendingRecord){Message("Stop the current action before changing this setting.");return true;}return false; }
        sealed class ModalScope : IDisposable {
            MainForm form;bool previous;
            public ModalScope(MainForm owner){form=owner;previous=form.changingShortcuts;form.changingShortcuts=true;}
            public void Dispose(){form.changingShortcuts=previous;}
        }
        void CancelPositionCapture(){if(positionCaptureTimer!=null){positionCaptureTimer.Stop();positionCaptureTimer.Dispose();positionCaptureTimer=null;}}
        protected override void Dispose(bool disposing){if(disposing){CancelPositionCapture();if(timer!=null)timer.Dispose();if(settingsWindow!=null)settingsWindow.Dispose();if(tips!=null)tips.Dispose();}base.Dispose(disposing);}
        void Message(string text) { LastMessage=text; if(!SelfTestMode) MessageBox.Show(ModalOwner,text,"Auto Loop",MessageBoxButtons.OK,MessageBoxIcon.Information); }
        void SetStatus(string text,string extra) {LastMessage=text;status.Text=text;detail.Text=extra;tips.SetToolTip(status,extra);}
        void RefreshWindows() {
            IntPtr selected=targets.SelectedItem is WindowChoice?((WindowChoice)targets.SelectedItem).Handle:IntPtr.Zero; targets.Items.Clear();
            Native.EnumWindows(delegate(IntPtr w,IntPtr p){string title=Native.Title(w); if(w!=Handle&&Native.IsWindowVisible(w)&&title.Length>0&&Native.ProcessId(w)!=(uint)Process.GetCurrentProcess().Id)targets.Items.Add(new WindowChoice{Handle=w,Process=Native.ProcessId(w),Title=title});return true;},IntPtr.Zero);
            for(int i=0;i<targets.Items.Count;i++) if(((WindowChoice)targets.Items[i]).Handle==selected) targets.SelectedIndex=i;
        }
        bool BindTarget() {
            boundTarget=null; if(!lockTarget.Checked)return true;
            WindowChoice target=targets.SelectedItem as WindowChoice;
            if(target==null||!Native.IsWindow(target.Handle)||Native.ProcessId(target.Handle)!=target.Process){Message("Choose an open target app. Use Refresh apps if it is missing.");return false;}
            boundTarget=target;return true;
        }
        bool FocusEligible() {
            if(TestFocus.HasValue)return TestFocus.Value;
            IntPtr foreground=Native.GetForegroundWindow();
            if(foreground==Handle||Native.ProcessId(foreground)==(uint)Process.GetCurrentProcess().Id)return false;
            return boundTarget==null || (Native.IsWindow(boundTarget.Handle)&&Native.ProcessId(boundTarget.Handle)==boundTarget.Process&&foreground==boundTarget.Handle);
        }
        bool ShortcutHeld() {
            foreach(int binding in preferences.Hotkeys) if((Native.GetAsyncKeyState(binding & 0xFFFF)&0x8000)!=0)return true;
            return (Native.GetAsyncKeyState(16)&0x8000)!=0||(Native.GetAsyncKeyState(17)&0x8000)!=0||(Native.GetAsyncKeyState(18)&0x8000)!=0||(Native.GetAsyncKeyState(91)&0x8000)!=0||(Native.GetAsyncKeyState(92)&0x8000)!=0;
        }
        void StartCountdown(bool rec, bool once=false) {
            if(running||recording||pendingPlay||pendingRecord)return;
            CancelPositionCapture();
            if(rec&&(keyboardHook==IntPtr.Zero||mouseHook==IntPtr.Zero)){Message("Recording is unavailable because the input hooks could not be installed. Restart Auto Loop.");return;}
            if(!BindTarget())return;
            if(!rec&&!ValidatePlayback())return;
            if(!rec)WindowState=FormWindowState.Minimized;
            pendingSingleRun=once;pendingRecord=rec;pendingPlay=!rec;SetEditingEnabled(false);countdownUntil=Stopwatch.GetTimestamp()+3L*Stopwatch.Frequency;
            SetStatus(rec?"Recording in 3 seconds":"Starting in 3 seconds",boundTarget==null?"Switch to the app you want to use. Stop cancels the countdown.":"Switch to the selected app. Stop cancels the countdown.");
        }
        bool ValidatePlayback() {
            if(modes.SelectedIndex==2) {
                if(recorded.Count==0){Message("Record a routine or open a saved recording first.");return false;}
                try{ValidateSteps(recorded,true);}catch(Exception e){Message(e.Message);return false;}
                HashSet<int> pressed=new HashSet<int>();
                foreach(Step step in recorded){if(step.Kind=="KeyDown"){pressed.Add(step.Code);if(preferences.Hotkeys.Any(b=>(b&0xFFFF)==step.Code&&BindingModifiers(b)==PressedModifiers(pressed))){Message("The recording contains a control shortcut. Change that shortcut before playback.");return false;}}else if(step.Kind=="KeyUp")pressed.Remove(step.Code);}
            } else {
                if(hold.Value>interval.Value){Message("Hold time must be shorter than or equal to the interval.");return false;}
                if(modes.SelectedIndex==1 && preferences.Hotkeys.Any(k=>(k & 0xFFFF)==actionKey)){Message("The repeated key matches a control shortcut. Choose a different key or shortcut.");return false;}
            }
            return true;
        }
        void ResetClock() {wall.Restart();lastWall=TestTime??0;activeClock=0;wasEligible=FocusEligible()&&!userPaused;}
        void AdvanceClock() {
            long now=TestTime??wall.ElapsedMilliseconds;bool eligible=FocusEligible()&&!userPaused;
            if(eligible&&wasEligible)activeClock+=Math.Max(0,now-lastWall);
            lastWall=now;wasEligible=eligible;
        }
        void BeginPlayback(bool once=false) {
            playback=new List<Step>();
            if(modes.SelectedIndex==2)playback=recorded.Select(Clone).ToList();
            else {
                bool click=modes.SelectedIndex==0;int code=click?new int[]{(int)MouseButtons.Left,(int)MouseButtons.Right,(int)MouseButtons.Middle}[mouse.SelectedIndex]:actionKey;
                if(click&&fixedPosition.Checked)playback.Add(new Step{Kind="Move",X=(int)px.Value,Y=(int)py.Value});
                playback.Add(new Step{Kind=click?"MouseDown":"KeyDown",Code=code,Extended=!click&&Native.Extended(code)});
                playback.Add(new Step{Kind=click?"MouseUp":"KeyUp",Code=code,Delay=(int)hold.Value,Extended=!click&&Native.Extended(code)});
                playback.Add(new Step{Kind="Wait",Delay=(int)(interval.Value-hold.Value)});
            }
            repeatCount=once?1:(int)repeats.Value;maxDuration=once?0:(long)limit.Value*1000;completed=0;index=0;due=playback[0].Delay;running=true;userPaused=false;focusPaused=false;ResetClock();
            SetStatus(once?"Testing once":"Running","Stop: "+KeyName(preferences.Hotkeys[3])+(boundTarget==null?". Input follows the active app.":". Input is restricted to the selected app."));
        }
        static Step Clone(Step s){return new Step{Kind=s.Kind,Code=s.Code,X=s.X,Y=s.Y,Delay=s.Delay,Extended=s.Extended};}
        void ToggleRecord() {if(recording){FinishRecording();return;}if(running||pendingPlay||pendingRecord)return;StartCountdown(true);}
        void BeginRecording() {
            recorded.Clear();recordKeys.Clear();recordMouse.Clear();recordedAt=0;lastMove=-100;recording=true;userPaused=false;focusPaused=false;modes.SelectedIndex=2;ResetClock();UpdateEvents();
            SetStatus("Recording","Capturing physical keys, clicks, scrolling and screen positions outside Auto Loop. Use Record again to finish.");
        }
        void FinishRecording() {
            if(!recording)return;AdvanceClock();recording=false; BalanceRecording();UpdateEvents();
            SetEditingEnabled(true);
            SetStatus("Recording saved in memory",recorded.Count+" steps. Use Save routine to keep it. Playback uses the recorded screen positions.");
        }
        void BalanceRecording() {
            int elapsed=(int)Math.Min(3600000,Math.Max(0,activeClock-recordedAt));
            foreach(int k in recordKeys.ToArray()){recorded.Add(new Step{Kind="KeyUp",Code=k,Extended=Native.Extended(k),Delay=elapsed});elapsed=0;}
            foreach(int b in recordMouse.ToArray()){recorded.Add(new Step{Kind="MouseUp",Code=b,Delay=elapsed});elapsed=0;}
            if(elapsed>0)recorded.Add(new Step{Kind="Wait",Delay=elapsed});
            recordedAt=activeClock;
            recordKeys.Clear();recordMouse.Clear();
        }
        void TogglePause() {
            if(!running&&!recording)return;
            AdvanceClock();userPaused=!userPaused;focusPauseReason="";ReleaseAll();
            if(recording)BalanceRecording();else {index=0;due=activeClock+playback[0].Delay;}
            if(!userPaused){focusPaused=false;wasEligible=false;}
            SetStatus(userPaused?"Paused":"Resuming",userPaused?"Press Pause again to resume, or Stop all to finish.":"The routine continues when the target app is active.");
        }
        void StopAll(string reason) {
            CancelPositionCapture();
            pendingPlay=false;pendingRecord=false;pendingSingleRun=false;focusPauseReason="";
            if(recording)FinishRecording();running=false;recording=false;userPaused=false;focusPaused=false;wall.Stop();ReleaseAll();
            SetEditingEnabled(true);
            SetStatus(reason,"All generated keys and mouse buttons have been released.");
        }
        void TickEngine() {
            if(busy)return;busy=true;
            try {
                if(pendingPlay||pendingRecord) {
                    long remaining=countdownUntil-Stopwatch.GetTimestamp();
                    if(remaining>0){status.Text=(pendingRecord?"Recording in ":"Starting in ")+Math.Ceiling(remaining/(double)Stopwatch.Frequency)+" seconds";return;}
                    if(ShortcutHeld()){status.Text="Release the shortcut keys to continue";return;}
                    bool rec=pendingRecord,once=pendingSingleRun;pendingPlay=false;pendingRecord=false;pendingSingleRun=false;if(rec)BeginRecording();else BeginPlayback(once);
                }
                if(!running&&!recording)return;
                if(boundTarget!=null&&(!Native.IsWindow(boundTarget.Handle)||Native.ProcessId(boundTarget.Handle)!=boundTarget.Process)){StopAll("Target app closed — stopped");return;}
                AdvanceClock();bool focus=FocusEligible();
                if(!focus||userPaused) {
                    if(!focus&&!focusPaused){focusPauseReason=boundTarget==null?"Auto Loop is active":"selected app is not active";focusPaused=true;ReleaseAll();if(recording)BalanceRecording();else {index=0;due=activeClock+playback[0].Delay;}if(!autoResume.Checked)userPaused=true;}
                    SetStatus(focusPauseReason.Length>0?"Paused: "+focusPauseReason:"Paused by you",userPaused?"Press "+KeyName(preferences.Hotkeys[2])+" to resume, or "+KeyName(preferences.Hotkeys[3])+" to stop.":"Return to the selected app to continue. Stop: "+KeyName(preferences.Hotkeys[3])+".");return;
                }
                if(focusPaused){if(ShortcutHeld())return;focusPaused=false;focusPauseReason="";}
                if(recording){status.Text="Recording  ·  "+recorded.Count+" steps";return;}
                if(maxDuration>0&&activeClock>=maxDuration){StopAll("Time limit reached");return;}
                int budget=0;
                while(running&&activeClock>=due&&budget++<100) {
                    if(!FocusEligible()){ReleaseAll();break;}
                    Execute(playback[index]);index++;
                    if(index==playback.Count){ReleaseAll();completed++;if(repeatCount>0&&completed>=repeatCount){StopAll("Finished  ·  "+completed+" repeats");return;}index=0;due=Math.Max(activeClock,due)+playback[0].Delay;if(playback.All(s=>s.Delay==0))due+=10;continue;}
                    due+=playback[index].Delay;
                }
                status.Text="Running  ·  "+completed+(repeatCount>0?" / "+repeatCount:"")+" repeats";
            } catch(Exception e){StopAll("Stopped — input could not be sent");Message(e.Message);}finally{busy=false;}
        }
        void Execute(Step s) {
            if(s.Kind=="KeyDown"){Send(Native.Key(s.Code,false,s.Extended));heldKeys[s.Code]=s.Extended;}
            else if(s.Kind=="KeyUp"){Send(Native.Key(s.Code,true,s.Extended));heldKeys.Remove(s.Code);}
            else if(s.Kind=="MouseDown"){Send(Native.Mouse(s.Code,false));heldMouse.Add(s.Code);}
            else if(s.Kind=="MouseUp"){Send(Native.Mouse(s.Code,true));heldMouse.Remove(s.Code);}
            else if(s.Kind=="Move")Send(Native.Move(s.X,s.Y));
            else if(s.Kind=="Wheel"){Native.INPUT i=new Native.INPUT();i.u.mi.flags=0x800;i.u.mi.mouseData=unchecked((uint)s.Code);Send(i);}
        }
        void Send(Native.INPUT input){if(InputSink!=null)InputSink(input);else Native.Send(input);}
        void ReleaseAll() {
            foreach(var k in heldKeys.Reverse().ToArray()){try{Send(Native.Key(k.Key,true,k.Value));}catch{}}
            foreach(int b in heldMouse.ToArray()){try{Send(Native.Mouse(b,true));}catch{}}
            heldKeys.Clear();heldMouse.Clear();
        }
        void InstallHooks() {
            keyboardProc=KeyboardHook;mouseProc=MouseHook;IntPtr module=Native.GetModuleHandle(null);
            keyboardHook=Native.SetWindowsHookEx(13,keyboardProc,module,0);mouseHook=Native.SetWindowsHookEx(14,mouseProc,module,0);
            if(keyboardHook==IntPtr.Zero||mouseHook==IntPtr.Zero){RemoveHooks();Message("Recording hooks could not be installed. Restart Auto Loop before recording.");recordButton.Enabled=false;}
        }
        void RemoveHooks(){if(keyboardHook!=IntPtr.Zero)Native.UnhookWindowsHookEx(keyboardHook);if(mouseHook!=IntPtr.Zero)Native.UnhookWindowsHookEx(mouseHook);keyboardHook=mouseHook=IntPtr.Zero;}
        bool CanRecord(){return recording&&!userPaused&&FocusEligible();}
        static int BindingModifiers(int binding){return binding&(int)Keys.Modifiers;}
        static int PressedModifiers(HashSet<int> keys){int mods=0;if(keys.Contains(16)||keys.Contains(160)||keys.Contains(161))mods|=(int)Keys.Shift;if(keys.Contains(17)||keys.Contains(162)||keys.Contains(163))mods|=(int)Keys.Control;if(keys.Contains(18)||keys.Contains(164)||keys.Contains(165))mods|=(int)Keys.Alt;return mods;}
        bool IsControlStroke(int key){int mods=(int)Control.ModifierKeys;return preferences.Hotkeys.Any(b=>(b&0xFFFF)==key&&BindingModifiers(b)==mods);}
        void StripControlModifiers(int binding){int mods=BindingModifiers(binding);for(int i=recorded.Count-1;i>=0;i--){Step s=recorded[i];if(s.Kind!="KeyDown"||!IsModifier(s.Code))break;bool used=((s.Code==16||s.Code==160||s.Code==161)&&(mods&(int)Keys.Shift)!=0)||((s.Code==17||s.Code==162||s.Code==163)&&(mods&(int)Keys.Control)!=0)||((s.Code==18||s.Code==164||s.Code==165)&&(mods&(int)Keys.Alt)!=0);if(!used)break;recordKeys.Remove(s.Code);s.Kind="Wait";}}
        void CaptureStep(Step s) {
            if(recorded.Count>=100000){BeginInvoke((Action)(()=>StopAll("Recording limit reached")));return;}
            AdvanceClock();s.Delay=(int)Math.Min(3600000,Math.Max(0,activeClock-recordedAt));recordedAt=activeClock;recorded.Add(s);
        }
        IntPtr KeyboardHook(int code,IntPtr message,IntPtr data) {
            try {
                if(code>=0) {
                    Native.KBD k=(Native.KBD)Marshal.PtrToStructure(data,typeof(Native.KBD));int key=(int)k.vkCode;int msg=message.ToInt32();
                    if((k.flags&0x10)==0&&CanRecord()) {
                        bool down=msg==0x100||msg==0x104;bool up=msg==0x101||msg==0x105;
                        if(down&&!IsControlStroke(key)){if(recordKeys.Add(key))CaptureStep(new Step{Kind="KeyDown",Code=key,Extended=(k.flags&1)!=0});}
                        else if(up&&recordKeys.Remove(key))CaptureStep(new Step{Kind="KeyUp",Code=key,Extended=(k.flags&1)!=0});
                    }
                }
            }catch{ /* Never allow hook exceptions to escape into Windows. */ }
            return Native.CallNextHookEx(keyboardHook,code,message,data);
        }
        IntPtr MouseHook(int code,IntPtr message,IntPtr data) {
            try {
                if(code>=0&&CanRecord()) {
                    Native.MOUSE m=(Native.MOUSE)Marshal.PtrToStructure(data,typeof(Native.MOUSE));if((m.flags&1)!=0)return Native.CallNextHookEx(mouseHook,code,message,data);
                    int msg=message.ToInt32(),button=0;bool down=false,up=false;
                    if(msg==0x200){AdvanceClock();if(activeClock-lastMove>=20){lastMove=activeClock;CaptureStep(new Step{Kind="Move",X=m.pt.X,Y=m.pt.Y});}}
                    else if(msg==0x20A){CaptureStep(new Step{Kind="Move",X=m.pt.X,Y=m.pt.Y});CaptureStep(new Step{Kind="Wheel",Code=(short)(m.data>>16)});}
                    else {
                        if(msg==0x201||msg==0x202){button=(int)MouseButtons.Left;down=msg==0x201;up=!down;}
                        if(msg==0x204||msg==0x205){button=(int)MouseButtons.Right;down=msg==0x204;up=!down;}
                        if(msg==0x207||msg==0x208){button=(int)MouseButtons.Middle;down=msg==0x207;up=!down;}
                        if(msg==0x20B||msg==0x20C){button=(m.data>>16)==1?(int)MouseButtons.XButton1:(int)MouseButtons.XButton2;down=msg==0x20B;up=!down;}
                        if(down&&recordMouse.Add(button)){CaptureStep(new Step{Kind="Move",X=m.pt.X,Y=m.pt.Y});CaptureStep(new Step{Kind="MouseDown",Code=button});}
                        if(up&&recordMouse.Remove(button)){CaptureStep(new Step{Kind="Move",X=m.pt.X,Y=m.pt.Y});CaptureStep(new Step{Kind="MouseUp",Code=button});}
                    }
                }
            }catch{ }
            return Native.CallNextHookEx(mouseHook,code,message,data);
        }
        void UpdateEvents(){events.BeginUpdate();events.Items.Clear();foreach(Step s in recorded)events.Items.Add(s);events.EndUpdate();recordCount.Text="Recorded routine · "+recorded.Count+" steps";}
        internal static string StepLabel(Step step){if(step.Kind=="KeyDown")return "Press "+KeyName(step.Code);if(step.Kind=="KeyUp")return "Release "+KeyName(step.Code);if(step.Kind=="Move")return "Move mouse";if(step.Kind=="MouseDown"||step.Kind=="MouseUp"){string button=((MouseButtons)step.Code).ToString().ToLowerInvariant();return (step.Kind=="MouseDown"?"Press ":"Release ")+button+" mouse button";}if(step.Kind=="Wait")return "Wait";if(step.Kind=="Wheel")return "Scroll mouse wheel";return step.Kind;}
        void DrawRecordedStep(object sender,DrawItemEventArgs e){if(e.Index<0||e.Index>=recorded.Count)return;Step step=recorded[e.Index];Color row=(e.State&DrawItemState.Selected)!=0?Color.FromArgb(39,66,70):field;using(Brush fill=new SolidBrush(row))e.Graphics.FillRectangle(fill,e.Bounds);string action=StepLabel(step);TextRenderer.DrawText(e.Graphics,(e.Index+1).ToString(),events.Font,new Rectangle(e.Bounds.X+5,e.Bounds.Y,26,e.Bounds.Height),muted,TextFormatFlags.VerticalCenter);TextRenderer.DrawText(e.Graphics,action,events.Font,new Rectangle(e.Bounds.X+37,e.Bounds.Y,Math.Max(0,e.Bounds.Width-112),e.Bounds.Height),ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);TextRenderer.DrawText(e.Graphics,step.Delay+" ms",events.Font,new Rectangle(e.Bounds.Right-74,e.Bounds.Y,68,e.Bounds.Height),muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Right);using(Pen line=new Pen(Color.FromArgb(39,49,63)))e.Graphics.DrawLine(line,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);}
        void EditDelay() {
            if(IsBusy()||events.SelectedIndex<0)return;int chosen=events.SelectedIndex;
            using(ModalScope scope=new ModalScope(this))using(ThemedDialog dialog=new ThemedDialog{Text="Delay before this step (ms)",ClientSize=new Size(350,175)}){
                Panel content=new Panel{Dock=DockStyle.Fill};NumberBox value=Number(0,3600000,recorded[chosen].Delay,280);value.Dock=DockStyle.Top;value.Height=35;RoundedButton save=new RoundedButton{Text="Save delay",BackColor=card,ForeColor=ink,Dock=DockStyle.Bottom,Height=35,DialogResult=DialogResult.OK};content.Controls.Add(value);content.Controls.Add(save);dialog.Controls.Add(content);dialog.AcceptButton=save;
                if(dialog.ShowDialog(ModalOwner)==DialogResult.OK){recorded[chosen].Delay=(int)value.Value;UpdateEvents();events.SelectedIndex=chosen;}
            }
        }
        bool RegisterBindings(int[] bindings) {
            for(int i=0;i<4;i++)Native.UnregisterHotKey(Handle,100+i);
            bool success=true;
            for(int i=0;i<4;i++){int b=bindings[i];uint mods=0x4000;if((b&(int)Keys.Control)!=0)mods|=2;if((b&(int)Keys.Alt)!=0)mods|=1;if((b&(int)Keys.Shift)!=0)mods|=4;if(!Native.RegisterHotKey(Handle,100+i,mods,(uint)(b&0xFFFF)))success=false;}
            if(!success)SetStatus("A shortcut is unavailable","Choose different shortcuts below. The Stop all button remains available.");return success;
        }
        void ChangeShortcut(int which) {
            if(IsBusy())return;bool previousShortcutState=changingShortcuts;changingShortcuts=true;
            try{using(ShortcutDialog d=new ShortcutDialog("Set "+new string[]{"Start","Record","Pause","Stop all"}[which]+" shortcut",preferences.Hotkeys[which])) {
                if(d.ShowDialog(ModalOwner)!=DialogResult.OK)return;
                int binding=d.Binding,key=binding&0xFFFF;if(key<8||key>254||IsModifier(key)){Message("Choose a letter, number, function key, or a combination.");return;}
                if(preferences.Hotkeys.Where((v,i)=>i!=which).Any(v=>(v&0xFFFF)==key)){Message("Each control needs a different main key, even when modifiers differ.");return;}
                if(key==actionKey){Message("That key is the repeated action key. Choose another shortcut.");return;}
                int[] next=(int[])preferences.Hotkeys.Clone();next[which]=binding;
                if(!RegisterBindings(next)){RegisterBindings(preferences.Hotkeys);Message("Windows or another app is already using that shortcut. Your previous shortcuts have been restored.");return;}
                preferences.Hotkeys=next;UpdateShortcuts();SavePreferences();SetStatus("Shortcuts updated","Your shortcut settings are saved beside Auto Loop.");
            }}finally{changingShortcuts=previousShortcutState;}
        }
        void UpdateShortcuts(){string[] names={"Start","Record","Pause","Stop all"};for(int i=0;i<4;i++)shortcuts[i].Text=names[i]+": "+KeyName(preferences.Hotkeys[i]);playButton.Text="Start · "+KeyName(preferences.Hotkeys[0]);recordButton.Text="Record · "+KeyName(preferences.Hotkeys[1]);pauseButton.Text="Pause · "+KeyName(preferences.Hotkeys[2]);stopButton.Text="Stop · "+KeyName(preferences.Hotkeys[3]);}
        protected override void WndProc(ref System.Windows.Forms.Message m) {
            if(m.Msg==0x312&&!changingShortcuts){int id=m.WParam.ToInt32()-100;if(id>=0&&id<4&&recording)StripControlModifiers(preferences.Hotkeys[id]);if(id==3)StopAll("Stopped");else if(id==0)StartCountdown(false);else if(id==1)ToggleRecord();else if(id==2)TogglePause();}
            base.WndProc(ref m);
        }
        Routine CurrentRoutine(){return new Routine{Mode=modes.SelectedIndex,Interval=(int)interval.Value,Hold=(int)hold.Value,Repeat=(int)repeats.Value,LimitSeconds=(int)limit.Value,Key=actionKey,MouseButton=mouse.SelectedIndex,FixedPosition=fixedPosition.Checked,FixedX=(int)px.Value,FixedY=(int)py.Value,LockTarget=lockTarget.Checked,AutoResume=autoResume.Checked,TargetTitle=targets.SelectedItem==null?"":targets.SelectedItem.ToString(),Steps=recorded.Select(Clone).ToList()};}
        void SaveRoutine() {
            if(IsBusy())return;using(ModalScope scope=new ModalScope(this)){try{string folder=Path.Combine(dataRoot,"Routines");Directory.CreateDirectory(folder);using(SaveFileDialog d=new SaveFileDialog{Filter="Auto Loop routine (*.autoloop)|*.autoloop",InitialDirectory=folder,DefaultExt="autoloop",AddExtension=true,FileName="My routine.autoloop"}){if(d.ShowDialog(ModalOwner)!=DialogResult.OK)return;WriteXml(d.FileName,CurrentRoutine());SetStatus("Routine saved",Path.GetFileName(d.FileName));}}catch(Exception e){Message("Could not save the routine: "+e.Message);}}
        }
        public static void WriteXml<T>(string path,T value) {
            string temp=path+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write))new XmlSerializer(typeof(T)).Serialize(stream,value);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        public static T ReadXml<T>(string path) {
            FileInfo info=new FileInfo(path);if(info.Length>30*1024*1024)throw new InvalidDataException("File exceeds the 30 MB limit.");
            using(XmlReader reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=30*1024*1024}))return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }
        void LoadRoutine(){if(IsBusy())return;using(ModalScope scope=new ModalScope(this)){using(OpenFileDialog d=new OpenFileDialog{Filter="Auto Loop routine (*.autoloop)|*.autoloop",InitialDirectory=Path.Combine(dataRoot,"Routines")}){if(d.ShowDialog(ModalOwner)!=DialogResult.OK)return;try{CancelPositionCapture();ApplyRoutine(ReadXml<Routine>(d.FileName));SetStatus("Routine opened","Choose the target app again before starting. Screen positions must match the recording.");}catch(Exception e){Message("Could not open the routine: "+e.Message);}}}}
        public static void ValidateRoutine(Routine r) {
            if(r==null||r.Version!=1||r.Mode<0||r.Mode>2||r.Interval<10||r.Interval>3600000||r.Hold<1||r.Hold>60000||r.Hold>r.Interval||r.Repeat<0||r.Repeat>1000000||r.LimitSeconds<0||r.LimitSeconds>86400||r.Key<8||r.Key>254||r.MouseButton<0||r.MouseButton>2||Math.Abs((long)r.FixedX)>100000||Math.Abs((long)r.FixedY)>100000)throw new InvalidDataException("Routine settings are invalid or unsupported.");ValidateSteps(r.Steps,false);
        }
        public static void ValidateSteps(List<Step> steps,bool requireBalanced) {
            if(steps==null||steps.Count>100000)throw new InvalidDataException("Recording is missing or too large.");
            HashSet<int> keys=new HashSet<int>(),buttons=new HashSet<int>();
            foreach(Step s in steps){if(s==null||s.Delay<0||s.Delay>3600000||Math.Abs((long)s.X)>100000||Math.Abs((long)s.Y)>100000)throw new InvalidDataException("A recorded step has invalid timing or position.");
                if(s.Kind=="KeyDown"||s.Kind=="KeyUp"){if(s.Code<8||s.Code>254)throw new InvalidDataException("Invalid recorded key.");if(s.Kind=="KeyDown"){if(!keys.Add(s.Code))throw new InvalidDataException("Recording has duplicate key-down events.");}else if(!keys.Remove(s.Code)&&requireBalanced)throw new InvalidDataException("A key-up step has no matching key-down. Restore the pair or record again.");}
                else if(s.Kind=="MouseDown"||s.Kind=="MouseUp"){if(!new int[]{(int)MouseButtons.Left,(int)MouseButtons.Right,(int)MouseButtons.Middle,(int)MouseButtons.XButton1,(int)MouseButtons.XButton2}.Contains(s.Code))throw new InvalidDataException("Invalid mouse button.");if(s.Kind=="MouseDown"){if(!buttons.Add(s.Code))throw new InvalidDataException("Recording has duplicate mouse-down events.");}else if(!buttons.Remove(s.Code)&&requireBalanced)throw new InvalidDataException("A mouse-up step has no matching mouse-down. Restore the pair or record again.");}
                else if(s.Kind=="Wheel"){if(Math.Abs((long)s.Code)>12000)throw new InvalidDataException("Invalid scroll amount.");}
                else if(s.Kind!="Move"&&s.Kind!="Wait")throw new InvalidDataException("Unknown recorded action.");
            }
            if(requireBalanced&&(keys.Count>0||buttons.Count>0))throw new InvalidDataException("Recording has an unmatched held key or mouse button. Restore the pair or record again.");
        }
        void ApplyRoutine(Routine r){ValidateRoutine(r);modes.SelectedIndex=r.Mode;interval.Value=r.Interval;hold.Value=r.Hold;repeats.Value=r.Repeat;limit.Value=r.LimitSeconds;actionKey=r.Key;SelectActionKey(actionKey);mouse.SelectedIndex=r.MouseButton;fixedPosition.Checked=r.FixedPosition;px.Value=r.FixedX;py.Value=r.FixedY;lockTarget.Checked=r.LockTarget;autoResume.Checked=r.AutoResume;targets.SelectedIndex=-1;recorded=r.Steps.Select(Clone).ToList();SyncStopDisplay();UpdateEvents();}
        void LoadPreferences(){try{string path=Path.Combine(dataRoot,"settings.xml");if(File.Exists(path)){Preferences p=ReadXml<Preferences>(path);if(p.Hotkeys==null||p.Hotkeys.Length!=4||p.Hotkeys.Select(k=>k&0xFFFF).Distinct().Count()!=4||p.Hotkeys.Any(k=>(k&0xFFFF)<8||(k&0xFFFF)>254||IsModifier(k&0xFFFF)))throw new InvalidDataException();preferences=p;}}catch{preferences=new Preferences();}}
        void SavePreferences(){try{WriteXml(Path.Combine(dataRoot,"settings.xml"),preferences);}catch(Exception e){detail.Text="Settings could not be saved beside the app: "+e.Message;}}
        internal void VerifyRoutine(string input,string output){SelfTestMode=true;TestFocus=true;TestTime=0;List<Native.INPUT> sent=new List<Native.INPUT>();InputSink=sent.Add;ApplyRoutine(ReadXml<Routine>(input));if(!ValidatePlayback())throw new InvalidDataException(LastMessage);BeginPlayback();long duration=recorded.Sum(x=>(long)x.Delay),cap=Math.Max(1000,duration)*Math.Max(1,repeatCount)+10000;while(running&&TestTime.Value<cap){TickEngine();TestTime+=10;}if(running)throw new Exception("Routine did not finish");int downs=sent.Count(i=>i.type==0&&(i.u.mi.flags&2)!=0),ups=sent.Count(i=>i.type==0&&(i.u.mi.flags&4)!=0);File.WriteAllLines(output,new string[]{"Engine simulation of supplied routine (no desktop input sent)","Completed repeats: "+completed,"Left mouse presses: "+downs,"Left mouse releases: "+ups,"Held buttons remaining: "+heldMouse.Count,"Held keys remaining: "+heldKeys.Count});}
        public void RunInternalTests(string path){SelfTests.Run(this,path);}
        internal bool TestApply(Routine r){ApplyRoutine(r);return CurrentRoutine().Steps.Count==r.Steps.Count;}
        Point PositionInForm(Control control){Point point=Point.Empty;while(control!=null&&control!=this){point.Offset(control.Left,control.Top);control=control.Parent;}return point;}
        internal void SavePreviews(string path){StopAll("Ready");TestFocus=true;WindowState=FormWindowState.Normal;lockTarget.Checked=false;stopRule.SelectedIndex=2;SelectActionKey(70);Show();Application.DoEvents();for(int mode=0;mode<3;mode++){modes.SelectedIndex=mode;if(mode==2){recorded=new List<Step>{new Step{Kind="Move",X=320,Y=240},new Step{Kind="MouseDown",Code=(int)MouseButtons.Left},new Step{Kind="Wait",Delay=500},new Step{Kind="MouseUp",Code=(int)MouseButtons.Left}};UpdateEvents();}PerformLayout();Application.DoEvents();using(Bitmap bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(path,new string[]{"Click","Keys","Record"}[mode]+".png"));}}lockTarget.Checked=true;PerformLayout();Application.DoEvents();using(Bitmap bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(path,"Selected app.png"));}settingsWindow.Show(this);Application.DoEvents();using(Bitmap bitmap=new Bitmap(settingsWindow.Width,settingsWindow.Height)){settingsWindow.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(path,"Settings.png"));}settingsWindow.Hide();}
        internal string[] LayoutDiagnostics(){List<string> lines=new List<string>();Action<Control,int> walk=null;walk=delegate(Control control,int depth){lines.Add(new string(' ',depth*2)+control.GetType().Name+" Text="+control.Text+" Bounds="+control.Bounds+" Visible="+control.Visible);foreach(Control child in control.Controls)walk(child,depth+1);};PerformLayout();walk(this,0);return lines.ToArray();}
        internal void TestEngine(Action<string,Action> check) {
            SelfTestMode=true;TestFocus=true;TestTime=0;List<Native.INPUT> sent=new List<Native.INPUT>();InputSink=sent.Add;
            Action reset=delegate{StopAll("Test reset");sent.Clear();TestFocus=true;TestTime=0;boundTarget=null;lockTarget.Checked=false;preferences=new Preferences();modes.SelectedIndex=1;actionKey=70;interval.Value=100;hold.Value=30;repeats.Value=3;limit.Value=0;autoResume.Checked=true;};
            check("Engine repeats exactly and respects interval",()=>{reset();BeginPlayback();TickEngine();if(sent.Count!=1)throw new Exception("Missing initial down");TestTime=30;TickEngine();if(sent.Count!=2)throw new Exception("Missing release");TestTime=100;TickEngine();if(sent.Count!=3)throw new Exception("Repeat did not start at interval");TestTime=130;TickEngine();TestTime=200;TickEngine();TestTime=230;TickEngine();TestTime=300;TickEngine();if(running||sent.Count!=6||completed!=3)throw new Exception("Incorrect repeat total");});
            check("Stop releases a held key",()=>{reset();BeginPlayback();TickEngine();StopAll("Test stop");if(sent.Count!=2||sent[1].u.ki.flags!=2||heldKeys.Count!=0||running)throw new Exception("Key remained down");});
            check("Focus loss releases and pauses; returning resumes",()=>{reset();BeginPlayback();TickEngine();TestFocus=false;TestTime=10;TickEngine();if(sent.Count!=2||!focusPaused||heldKeys.Count!=0)throw new Exception("Focus loss failed");TestTime=500;TickEngine();if(sent.Count!=2)throw new Exception("Input leaked while unfocused");TestFocus=true;TestTime=600;TickEngine();if(sent.Count!=3||focusPaused)throw new Exception("Resume failed");});
            check("Automatic resume can be disabled",()=>{reset();autoResume.Checked=false;BeginPlayback();TickEngine();TestFocus=false;TestTime=10;TickEngine();TestFocus=true;TestTime=1000;TickEngine();if(!userPaused||sent.Count!=2)throw new Exception("Unexpected resume");});
            check("Time limit stops and releases",()=>{reset();repeats.Value=0;limit.Value=1;BeginPlayback();TickEngine();TestTime=1001;TickEngine();if(running||heldKeys.Count!=0||sent.Count!=2)throw new Exception("Time limit failed");});
            check("Input failure stops playback",()=>{reset();InputSink=delegate{throw new InvalidOperationException("Test blocked input");};BeginPlayback();TickEngine();InputSink=sent.Add;if(running)throw new Exception("Playback continued after failure");});
            check("Recording preserves unfinished held duration",()=>{reset();BeginRecording();recordKeys.Add(70);CaptureStep(new Step{Kind="KeyDown",Code=70});TestTime=2000;FinishRecording();if(recorded.Count!=2||recorded[1].Delay!=2000||recorded[1].Kind!="KeyUp")throw new Exception("Hold duration lost");ValidateSteps(recorded,true);});
            check("Injected keyboard events are ignored by recorder",()=>{reset();BeginRecording();IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.KBD)));try{Marshal.StructureToPtr(new Native.KBD{vkCode=70,flags=0x10},p,false);KeyboardHook(0,new IntPtr(0x100),p);if(recorded.Count!=0)throw new Exception("Injected key recorded");}finally{Marshal.FreeHGlobal(p);}FinishRecording();});
            check("Keyboard hook records physical key pairs",()=>{reset();BeginRecording();IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.KBD)));try{Marshal.StructureToPtr(new Native.KBD{vkCode=70},p,false);KeyboardHook(0,new IntPtr(0x100),p);TestTime=75;KeyboardHook(0,new IntPtr(0x101),p);if(recorded.Count!=2||recorded[1].Delay!=75)throw new Exception("Physical event fixture failed");}finally{Marshal.FreeHGlobal(p);}FinishRecording();ValidateSteps(recorded,true);});
            check("Mouse hook preserves drag release position",()=>{reset();BeginRecording();IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.MOUSE)));try{Native.MOUSE m=new Native.MOUSE{pt=new Native.POINT{X=10,Y=20}};Marshal.StructureToPtr(m,p,false);MouseHook(0,new IntPtr(0x201),p);TestTime=5;m.pt.X=100;m.pt.Y=200;Marshal.StructureToPtr(m,p,false);MouseHook(0,new IntPtr(0x202),p);if(recorded.Count!=4||recorded[2].Kind!="Move"||recorded[2].X!=100||recorded[3].Kind!="MouseUp")throw new Exception("Endpoint lost");}finally{Marshal.FreeHGlobal(p);}FinishRecording();ValidateSteps(recorded,true);});
            check("Recorded double-click replays both button pairs",()=>{reset();BeginRecording();IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.MOUSE)));try{Marshal.StructureToPtr(new Native.MOUSE{pt=new Native.POINT{X=100,Y=200}},p,false);MouseHook(0,new IntPtr(0x201),p);TestTime=40;MouseHook(0,new IntPtr(0x202),p);TestTime=120;MouseHook(0,new IntPtr(0x201),p);TestTime=160;MouseHook(0,new IntPtr(0x202),p);}finally{Marshal.FreeHGlobal(p);}FinishRecording();ValidateSteps(recorded,true);repeats.Value=1;BeginPlayback();TickEngine();foreach(long t in new long[]{40,120,160,200}){TestTime=t;TickEngine();}uint[] buttons=sent.Where(i=>i.type==0&&(i.u.mi.flags&6)!=0).Select(i=>i.u.mi.flags).ToArray();if(!buttons.SequenceEqual(new uint[]{2,4,2,4})||running)throw new Exception("Double-click button events lost");});
            check("Playback minimizes only after validation; recording stays visible",()=>{reset();WindowState=FormWindowState.Normal;lockTarget.Checked=false;modes.SelectedIndex=1;StartCountdown(false);if(!pendingPlay||WindowState!=FormWindowState.Minimized)throw new Exception("Playback did not minimize");StopAll("Test stop");WindowState=FormWindowState.Normal;modes.SelectedIndex=2;recorded.Clear();StartCountdown(false);if(pendingPlay||WindowState!=FormWindowState.Normal)throw new Exception("Invalid playback minimized");reset();WindowState=FormWindowState.Normal;lockTarget.Checked=false;IntPtr oldKeyboard=keyboardHook,oldMouse=mouseHook;try{keyboardHook=mouseHook=new IntPtr(1);StartCountdown(true);if(!pendingRecord||WindowState!=FormWindowState.Normal)throw new Exception("Recording unexpectedly minimized");}finally{keyboardHook=oldKeyboard;mouseHook=oldMouse;StopAll("Test stop");}});
            check("App scope switch shows and hides picker without losing target",()=>{reset();lockTarget.Checked=true;PerformLayout();if(ClientSize!=new Size(400,587)||!targets.Parent.Visible&&Visible)throw new Exception("Selected app controls missing");lockTarget.Checked=false;PerformLayout();if(ClientSize!=new Size(400,512)||targets.Parent.Visible)throw new Exception("Any app did not collapse picker");if(!BindTarget()||boundTarget!=null)throw new Exception("Any app requires a target");});
            check("Option 2 keeps app scope inside the configuration panel",()=>{reset();if(targets.Parent.Parent.Parent!=optionGrid.Parent)throw new Exception("App selector is outside the main panel");});
            check("Key capture matches mouse field geometry",()=>{reset();if(keyCapture.Bounds!=mouse.Bounds||keyCapture.Font.Size!=mouse.Font.Size||keyCapture.TextAlign!=ContentAlignment.MiddleLeft)throw new Exception("Key field differs from mouse field");});
            check("Action controls fit equally and status is below them",()=>{reset();PerformLayout();Control actions=playButton.Parent;foreach(Control button in actions.Controls){if(Math.Abs(button.Width-playButton.Width)>6||button.Height!=playButton.Height||button.Right>actions.Width||button.Bottom>actions.Height)throw new Exception("Action button size or clipping");}if(PositionInForm(status).Y<PositionInForm(playButton).Y+playButton.Height)throw new Exception("Status is above actions");});
            check("Test once ignores repeat and duration settings without changing them",()=>{reset();repeats.Value=10;limit.Value=1;BeginPlayback(true);TickEngine();TestTime=30;TickEngine();TestTime=100;TickEngine();if(running||completed!=1||sent.Count!=2||repeats.Value!=10||limit.Value!=1)throw new Exception("Single-run behavior or saved settings changed");});
            check("Test once supports continuous setting and normal playback afterwards",()=>{reset();repeats.Value=0;BeginPlayback(true);TickEngine();TestTime=30;TickEngine();TestTime=100;TickEngine();if(running||completed!=1)throw new Exception("Test did not finish");repeats.Value=3;BeginPlayback();if(repeatCount!=3)throw new Exception("Test mode leaked into normal playback");StopAll("Done");});
            check("Recording labels distinguish button press and release",()=>{if(StepLabel(new Step{Kind="MouseDown",Code=(int)MouseButtons.Left})!="Press left mouse button"||StepLabel(new Step{Kind="MouseUp",Code=(int)MouseButtons.Left})!="Release left mouse button")throw new Exception("Mouse labels ambiguous");});
            check("Pause status explains focus loss and manual pause",()=>{reset();BeginPlayback();TestFocus=false;TestTime=10;TickEngine();if(status.Text!="Paused: Auto Loop is active")throw new Exception("Missing focus explanation");StopAll("Done");reset();BeginPlayback();TogglePause();TickEngine();if(status.Text!="Paused by you")throw new Exception("Missing manual pause explanation");});
            check("Recording toolbar captions fit without ellipsis",()=>{reset();modes.SelectedIndex=2;PerformLayout();recordingTools.PerformLayout();foreach(Control button in recordingTools.Controls)if(TextRenderer.MeasureText(button.Text,button.Font).Width>button.Width-20)throw new Exception("Truncated toolbar caption: "+button.Text);});
            check("Recorded control shortcut conflict is rejected",()=>{reset();modes.SelectedIndex=2;recorded=new List<Step>{new Step{Kind="KeyDown",Code=120},new Step{Kind="KeyUp",Code=120,Delay=20}};if(ValidatePlayback())throw new Exception("Stop shortcut accepted as playback");});
            check("Plain letter does not conflict with modified shortcut",()=>{reset();preferences.Hotkeys[0]=(int)(Keys.Control|Keys.S);modes.SelectedIndex=2;recorded=new List<Step>{new Step{Kind="KeyDown",Code=83},new Step{Kind="KeyUp",Code=83,Delay=20}};if(!ValidatePlayback())throw new Exception("Plain letter incorrectly blocked");});
            check("Click and Keys keep the same compact size",()=>{reset();for(int i=0;i<20;i++){modes.SelectedIndex=i%2;PerformLayout();if(ClientSize!=new Size(400,512))throw new Exception("Mode changed compact size");}});
            check("Every alphabet key is selectable",()=>{reset();for(int key=65;key<=90;key++){SelectActionKey(key);if(actionKey!=key||((KeyChoice)keyChoices.SelectedItem).Code!=key)throw new Exception("Key selection failed");}});
            check("Record toolbar fits without clipped buttons",()=>{reset();modes.SelectedIndex=2;PerformLayout();compactRoot.PerformLayout();recordingHost.PerformLayout();recordingTools.PerformLayout();if(recordingTools.Controls.Count!=4)throw new Exception("Missing editing tools");foreach(Control control in recordingTools.Controls)if(control.Bottom>recordingTools.ClientSize.Height||control.Height<30||control.Width<50)throw new Exception("Clipped recording tool");modes.SelectedIndex=0;if(ClientSize.Height!=512)throw new Exception("Record size did not restore");});
            check("Nested modal scope preserves shortcut suppression",()=>{reset();changingShortcuts=false;using(ModalScope outer=new ModalScope(this)){using(ModalScope inner=new ModalScope(this)){}if(!changingShortcuts)throw new Exception("Nested guard restored too early");System.Windows.Forms.Message message=System.Windows.Forms.Message.Create(Handle,0x312,new IntPtr(100),IntPtr.Zero);WndProc(ref message);if(pendingPlay)throw new Exception("Shortcut started during modal");}if(changingShortcuts)throw new Exception("Guard remained enabled");});
            check("Loop Arrow logo is embedded",()=>{using(Stream resource=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoLoop.Logo")){if(resource==null||resource.Length==0)throw new Exception("Logo resource missing");}});
            check("Stop after repeat count updates engine",()=>{reset();stopRule.SelectedIndex=0;stopValue.Value=7;if(repeats.Value!=7||limit.Value!=0)throw new Exception("Count did not reach engine");});
            check("Stop after duration updates engine",()=>{reset();stopRule.SelectedIndex=1;stopValue.Value=5;if(repeats.Value!=0||limit.Value!=5)throw new Exception("Duration did not reach engine");});
            check("Until stopped clears both limits",()=>{reset();stopRule.SelectedIndex=2;if(repeats.Value!=0||limit.Value!=0)throw new Exception("Limits remained active");});
            check("Imported dual limits remain accurate and cannot become stale",()=>{reset();repeats.Value=10;limit.Value=2;SyncStopDisplay();if(stopRule.SelectedIndex!=3||repeats.Value!=10||limit.Value!=2)throw new Exception("Dual limits lost");stopRule.SelectedIndex=2;if(stopRule.Items.Count!=3||repeats.Value!=0||limit.Value!=0)throw new Exception("Stale legacy option remained");});
            check("All three tabs preserve window size",()=>{reset();for(int i=0;i<15;i++){modes.SelectedIndex=i%3;PerformLayout();if(ClientSize!=new Size(400,512))throw new Exception("Tab resized the window");}});
            check("Approved control styling is installed",()=>{reset();if(!(modeTabs[0] is RoundedButton)||!(status is StatusLabel)||!(targets is DarkComboBox)||!(lockTarget is MintCheckBox))throw new Exception("A themed control is missing");});
            check("Typed numeric input reaches the engine value",()=>{using(NumberBox number=new NumberBox{Minimum=10,Maximum=1000,Value=100}){TextBox text=number.Controls.OfType<TextBox>().Single();text.Text="250";if(number.Value!=250)throw new Exception("Typed value not committed");text.Text="9000";if(number.Value!=1000)throw new Exception("Out-of-range value not bounded");text.Text="";if(number.Value!=1000)throw new Exception("Blank entry lost prior value");}});
            check("Negative coordinates remain editable",()=>{using(NumberBox number=new NumberBox{Minimum=-100000,Maximum=100000,Value=0}){number.Controls.OfType<TextBox>().Single().Text="-1234";if(number.Value!=-1234)throw new Exception("Negative position lost");}});
            check("Interval and Stop after share the reference input column",()=>{reset();for(int mode=0;mode<2;mode++){modes.SelectedIndex=mode;PerformLayout();compactRoot.PerformLayout();optionGrid.PerformLayout();Control stopField=stopValue.Parent.Parent;if(PositionInForm(interval).X!=PositionInForm(stopField).X)throw new Exception("Input fields are misaligned");if(interval.Width<140)throw new Exception("Interval width differs from reference");if(PositionInForm(interval.Controls.OfType<TextBox>().Single()).X!=PositionInForm(stopValue.Controls.OfType<TextBox>().Single()).X)throw new Exception("Input text is misaligned");Label intervalCaption=interval.Parent.Controls.OfType<Label>().First(c=>c.Text=="Interval");TableLayoutPanel stopRow=stopField.Parent as TableLayoutPanel;Control stopCaption=stopRow.GetControlFromPosition(0,0);if(PositionInForm(intervalCaption).X!=PositionInForm(stopCaption).X||intervalCaption.ForeColor!=stopCaption.ForeColor)throw new Exception("Row captions differ in alignment or color");}});
            reset();InputSink=null;TestFocus=null;TestTime=null;
        }
    }
    internal static class SelfTests {
        public static void Run(MainForm form,string path) {
            List<string> results=new List<string>();Action<string,Action> check=delegate(string name,Action action){try{action();results.Add("PASS "+name);}catch(Exception e){results.Add("FAIL "+name+": "+e.Message);}};
            check("64-bit native INPUT structure",()=>{if(Marshal.SizeOf(typeof(Native.INPUT))!=(IntPtr.Size==8?40:28))throw new Exception("Wrong structure size");});
            check("Settings and recording round trip",()=>{Routine r=new Routine{Mode=2,Repeat=3,Key=82,Steps=new List<Step>{new Step{Kind="KeyDown",Code=70},new Step{Kind="KeyUp",Code=70,Delay=30},new Step{Kind="Wait",Delay=100}}};string p=Path.Combine(path,"roundtrip.autoloop");MainForm.WriteXml(p,r);Routine q=MainForm.ReadXml<Routine>(p);MainForm.ValidateRoutine(q);if(q.Repeat!=3||q.Steps[1].Delay!=30||!form.TestApply(q))throw new Exception("Values changed");});
            check("Reject invalid timing",()=>{try{MainForm.ValidateRoutine(new Routine{Interval=-1});}catch(InvalidDataException){return;}throw new Exception("Accepted invalid settings");});
            check("Reject unmatched held key",()=>{try{MainForm.ValidateSteps(new List<Step>{new Step{Kind="KeyDown",Code=70}},true);}catch(InvalidDataException){return;}throw new Exception("Accepted held key");});
            check("Reject orphan release",()=>{try{MainForm.ValidateSteps(new List<Step>{new Step{Kind="MouseUp",Code=(int)MouseButtons.Left}},true);}catch(InvalidDataException){return;}throw new Exception("Accepted unmatched release");});
            check("Reject unknown action",()=>{try{MainForm.ValidateSteps(new List<Step>{new Step{Kind="Launch"}},true);}catch(InvalidDataException){return;}throw new Exception("Accepted unknown action");});
            check("Extended key flags",()=>{if(Native.Key(39,true,true).u.ki.flags!=3)throw new Exception("Wrong flags");});
            check("Mouse release flags",()=>{if(Native.Mouse((int)MouseButtons.Left,true).u.mi.flags!=4)throw new Exception("Wrong flags");});
            check("Virtual desktop coordinates",()=>{Rectangle r=SystemInformation.VirtualScreen;Native.INPUT i=Native.Move(r.Left,r.Top);if(i.u.mi.dx!=0||i.u.mi.dy!=0||(i.u.mi.flags&0x4000)==0)throw new Exception("Wrong origin");});
            form.TestEngine(check);
            form.SavePreviews(path);File.WriteAllLines(Path.Combine(path,"layout-diagnostics.txt"),form.LayoutDiagnostics());
            File.WriteAllLines(Path.Combine(path,"internal-tests.txt"),results.ToArray());
        }
    }
    static class Program {
        [STAThread] static void Main(string[] args) {
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==3&&args[0]=="--verify-routine"){using(MainForm form=new MainForm()){form.SelfTestMode=true;form.VerifyRoutine(args[1],args[2]);}return;}
            if(args.Length==2&&args[0]=="--self-test"){using(MainForm testForm=new MainForm()){testForm.SelfTestMode=true;testForm.RunInternalTests(args[1]);}return;}
            using(var mutex=new System.Threading.Mutex(false,"Local\\AutoLoopPortableSingleInstance")){
                if(!mutex.WaitOne(0,false)){MessageBox.Show("Auto Loop is already open.","Auto Loop");return;}
                try{MainForm form=new MainForm();if(args.Length==2&&args[0]=="--self-test"){form.SelfTestMode=true;form.RunInternalTests(args[1]);form.Dispose();return;}Application.Run(form);}finally{mutex.ReleaseMutex();}
            }
        }
    }
}




































