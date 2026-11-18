using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Steam Link Screen Selector")]
[assembly: AssemblyProduct("Steam Link Screen Selector")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

class App
{
 [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
 [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr m,string n);
 [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a,bool i,int pid);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr a,UIntPtr s,uint t,uint p);
 [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr h,IntPtr a,UIntPtr s,uint t);
 [DllImport("kernel32.dll")] static extern bool VirtualProtectEx(IntPtr h,IntPtr a,UIntPtr s,uint np,out uint op);
 [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr s,out UIntPtr w);
 [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h,IntPtr a,byte[] b,UIntPtr s,out UIntPtr r);
 [DllImport("kernel32.dll")] static extern bool FlushInstructionCache(IntPtr h,IntPtr a,UIntPtr s);
 [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr h,out bool w);

 [StructLayout(LayoutKind.Sequential)] struct LUID{public uint Lo;public int Hi;}
 [StructLayout(LayoutKind.Sequential)] struct SRC{public LUID ad;public uint id;public uint mi;}
 [StructLayout(LayoutKind.Sequential)] struct TGT{public LUID ad;public uint id;public uint mi;public uint ot;public uint rot;public uint scl;public uint rn;public uint rd;public uint slo;public uint av;public uint fl;}
 [StructLayout(LayoutKind.Sequential)] struct PATH{public SRC src;public uint pad;public TGT tgt;public uint fl;}
 [StructLayout(LayoutKind.Sequential,Size=64)] struct MODE{}
 [StructLayout(LayoutKind.Sequential)] struct HDR{public uint type;public uint size;public LUID ad;public uint id;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct SRCNAME{public HDR h;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string name;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct TGTNAME{public HDR h;public uint fl;public uint ot;public ushort man;public ushort prod;public uint conn;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string friendly;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string path;}
 [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint f,out uint np,out uint nm);
 [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint f,ref uint np,[Out]PATH[] pa,ref uint nm,[Out]MODE[] ma,IntPtr c);
 [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref SRCNAME r);
 [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref TGTNAME r);

 static Dictionary<string,string> Friendly(){
  var map=new Dictionary<string,string>();uint np,nm;
  if(GetDisplayConfigBufferSizes(2,out np,out nm)!=0)return map;
  var pa=new PATH[np];var ma=new MODE[nm];
  if(QueryDisplayConfig(2,ref np,pa,ref nm,ma,IntPtr.Zero)!=0)return map;
  for(int i=0;i<np;i++){
   var s=new SRCNAME();s.h.type=1;s.h.size=(uint)Marshal.SizeOf(typeof(SRCNAME));s.h.ad=pa[i].src.ad;s.h.id=pa[i].src.id;
   if(DisplayConfigGetDeviceInfo(ref s)!=0||string.IsNullOrEmpty(s.name))continue;
   var t=new TGTNAME();t.h.type=2;t.h.size=(uint)Marshal.SizeOf(typeof(TGTNAME));t.h.ad=pa[i].tgt.ad;t.h.id=pa[i].tgt.id;
   if(DisplayConfigGetDeviceInfo(ref t)==0&&!string.IsNullOrEmpty(t.friendly)&&!map.ContainsKey(s.name))map[s.name]=t.friendly;
  }
  return map;
 }

 const uint PROC=0x438,MEM=0x3000,RWX=0x40,RELEASE=0x8000;
 static IntPtr hProc=IntPtr.Zero,cave=IntPtr.Zero,gcp=IntPtr.Zero;
 static byte[] orig;
 static bool hooked=false;
 static int targetPid=0;
 static NotifyIcon Ni;
 static int[] curRect=null;

 static void Add(List<byte> l,params byte[] b){ l.AddRange(b); }

 static string Install(int[] r){
  var ps=Process.GetProcessesByName("steam");
  if(ps.Length==0)return "Steam is not running (no 'steam' process). Start Steam first.";
  targetPid=ps[0].Id;
  hProc=OpenProcess(PROC,false,targetPid);
  if(hProc==IntPtr.Zero){ int e=Marshal.GetLastWin32Error(); return "Cannot open Steam process (OpenProcess err "+e+"). "+(e==5?"Access denied - run this app as the SAME user as Steam, or as admin if Steam is elevated.":"Try running as admin."); }
  bool wow=false; try{ IsWow64Process(hProc,out wow); }catch{}
  if(wow){ CloseHandle(hProc); hProc=IntPtr.Zero; return "This Steam is 32-bit (WOW64). This tool only hooks the 64-bit Steam client. Update Steam to the current (64-bit) client."; }
  gcp=GetProcAddress(GetModuleHandle("user32.dll"),"GetCursorPos");
  var pre=new byte[24]; UIntPtr u;
  if(!ReadProcessMemory(hProc,gcp,pre,(UIntPtr)24,out u)){ int e=Marshal.GetLastWin32Error(); CloseHandle(hProc); hProc=IntPtr.Zero; return "ReadProcessMemory failed (err "+e+") at GetCursorPos. Likely a bitness/ASLR mismatch."; }
  int j=-1; for(int i=0;i<=16;i++){ if(pre[i]==0x48&&pre[i+1]==0xFF&&pre[i+2]==0x25){ j=i; break; } }
  if(j<0){ string hex=BitConverter.ToString(pre); CloseHandle(hProc); hProc=IntPtr.Zero; return "GetCursorPos prologue not recognized on this Windows build. Bytes: "+hex; }
  byte[] setup=new byte[j]; Array.Copy(pre,0,setup,0,j);
  int disp=BitConverter.ToInt32(pre,j+3);
  IntPtr wptr=(IntPtr)(gcp.ToInt64()+j+7+disp);
  var wb=new byte[8]; ReadProcessMemory(hProc,wptr,wb,(UIntPtr)8,out u);
  long worker=BitConverter.ToInt64(wb,0);
  cave=VirtualAllocEx(hProc,IntPtr.Zero,(UIntPtr)256,MEM,RWX);
  if(cave==IntPtr.Zero){ int e=Marshal.GetLastWin32Error(); CloseHandle(hProc); hProc=IntPtr.Zero; return "VirtualAllocEx failed (err "+e+") - cannot allocate code cave in Steam."; }
  WriteRect(r);
  long ds=cave.ToInt64();
  var c=new List<byte>();
  Add(c,0x48,0x83,0xEC,0x28); Add(c,0x48,0x89,0x4C,0x24,0x20); c.AddRange(setup);
  c.Add(0x48); c.Add(0xB8); c.AddRange(BitConverter.GetBytes(worker)); Add(c,0xFF,0xD0);
  Add(c,0x48,0x8B,0x4C,0x24,0x20); c.Add(0x48); c.Add(0xB8); c.AddRange(BitConverter.GetBytes(ds));
  Add(c,0x8B,0x11,0x44,0x8B,0x00,0x44,0x39,0xC2,0x41,0x0F,0x4C,0xD0,0x44,0x8B,0x48,0x08,0x41,0x83,0xE9,0x01,0x44,0x39,0xCA,0x41,0x0F,0x4F,0xD1,0x89,0x11);
  Add(c,0x8B,0x51,0x04,0x44,0x8B,0x40,0x04,0x44,0x39,0xC2,0x41,0x0F,0x4C,0xD0,0x44,0x8B,0x48,0x0C,0x41,0x83,0xE9,0x01,0x44,0x39,0xCA,0x41,0x0F,0x4F,0xD1,0x89,0x51,0x04);
  Add(c,0xB8,0x01,0x00,0x00,0x00,0x48,0x83,0xC4,0x28,0xC3);
  IntPtr codeAddr=(IntPtr)(ds+32);
  WriteProcessMemory(hProc,codeAddr,c.ToArray(),(UIntPtr)c.Count,out u);
  FlushInstructionCache(hProc,codeAddr,(UIntPtr)256);
  orig=new byte[14]; ReadProcessMemory(hProc,gcp,orig,(UIntPtr)14,out u);
  var patch=new List<byte>(); Add(patch,0xFF,0x25,0,0,0,0); patch.AddRange(BitConverter.GetBytes(codeAddr.ToInt64()));
  uint old; VirtualProtectEx(hProc,gcp,(UIntPtr)16,RWX,out old);
  WriteProcessMemory(hProc,gcp,patch.ToArray(),(UIntPtr)patch.Count,out u);
  VirtualProtectEx(hProc,gcp,(UIntPtr)16,old,out old);
  FlushInstructionCache(hProc,gcp,(UIntPtr)16);
  hooked=true; curRect=r;
  return null;
 }

 static void WriteRect(int[] r){ var b=new byte[16]; BitConverter.GetBytes(r[0]).CopyTo(b,0); BitConverter.GetBytes(r[1]).CopyTo(b,4); BitConverter.GetBytes(r[2]).CopyTo(b,8); BitConverter.GetBytes(r[3]).CopyTo(b,12); UIntPtr u; WriteProcessMemory(hProc,cave,b,(UIntPtr)16,out u); }

 static void Uninstall(){
  if(!hooked)return;
  try{ uint old; VirtualProtectEx(hProc,gcp,(UIntPtr)16,RWX,out old); UIntPtr u; WriteProcessMemory(hProc,gcp,orig,(UIntPtr)14,out u); VirtualProtectEx(hProc,gcp,(UIntPtr)16,old,out old); FlushInstructionCache(hProc,gcp,(UIntPtr)16); }catch{}
  try{ VirtualFreeEx(hProc,cave,UIntPtr.Zero,RELEASE); }catch{}
  try{ CloseHandle(hProc); }catch{}
  hProc=IntPtr.Zero; cave=IntPtr.Zero; hooked=false; curRect=null;
 }

 static bool Alive(){ try{ var p=Process.GetProcessById(targetPid); return p!=null && !p.HasExited && p.ProcessName.ToLower()=="steam"; }catch{ return false; } }

 static void SetMon(int[] r){
  if(hooked && !Alive()){ hProc=IntPtr.Zero; cave=IntPtr.Zero; hooked=false; curRect=null; }
  if(r==null){ Uninstall(); return; }
  if(!hooked){ string err=Install(r); if(err!=null){ Ni.ShowBalloonTip(6000,"Steam Link Screen Selector",err,ToolTipIcon.Warning); } }
  else { WriteRect(r); curRect=r; }
 }

 static void Build(ContextMenuStrip menu){
  menu.Items.Clear();
  var fn=Friendly();
  bool on=hooked && curRect!=null && Alive();
  if(!on && hooked){ hooked=false; curRect=null; }
  var def=(ToolStripMenuItem)menu.Items.Add("Default (follow cursor)");
  def.Checked=!on;
  def.Click+=(s,e)=>SetMon(null);
  int k=1;
  foreach(var sc in Screen.AllScreens){
   string dev=sc.DeviceName;
   string nm=fn.ContainsKey(dev)?fn[dev]:dev;
   var b=sc.Bounds;
   string lbl=string.Format("{0}: {1}  {2}x{3}{4}",k,nm,b.Width,b.Height,sc.Primary?" (primary)":"");
   var it=(ToolStripMenuItem)menu.Items.Add(lbl);
   it.Checked=on && curRect!=null && curRect[0]==b.Left && curRect[1]==b.Top && curRect[2]==b.Right && curRect[3]==b.Bottom;
   int[] r=new int[]{b.Left,b.Top,b.Right,b.Bottom};
   it.Click+=(s,e)=>SetMon(r);
   k++;
  }
  menu.Items.Add(new ToolStripSeparator());
  menu.Items.Add("Exit").Click+=(s,e)=>{ SetMon(null); Ni.Visible=false; Application.Exit(); };
 }

 [STAThread]
 static void Main(){
  bool created; var mx=new Mutex(true,"SteamLinkScreenSelectorSingleton",out created); if(!created)return;
  var menu=new ContextMenuStrip();
  menu.Opening+=(s,e)=>Build(menu);
  Ni=new NotifyIcon();
  try{ Ni.Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); }catch{ Ni.Icon=SystemIcons.Application; }
  Ni.Text="Steam Link Screen Selector";
  Ni.Visible=true;
  Ni.ContextMenuStrip=menu;
  Application.ApplicationExit+=(s,e)=>{ try{Uninstall();}catch{} };
  Application.Run();
  GC.KeepAlive(mx);
 }
}
