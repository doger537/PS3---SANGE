using HidSharp;
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using System.IO;
namespace Legacy;
public record Device(string Id,string Name,string Detail,HidDevice? Hid=null,int Slot=-1){public override string ToString()=>Name+"\n"+Detail;}
public interface IInputBackend:IDisposable { PadState Read(); void Rumble(byte large,byte small); }
public interface IVirtualBackend:IDisposable { void Send(PadState state); int UserIndex{get;} }
public sealed class Ds3Backend:IInputBackend {
 readonly HidStream stream; readonly byte[] buffer; readonly object gate=new();
 public Ds3Backend(HidDevice d){if(d.VendorID!=0x7331||d.ProductID!=1)throw new NotSupportedException("Для DS3 выберите DS4Windows mode в DsHidMini ControlApp. Другие HID layouts пока не поддерживаются.");stream=d.Open();stream.ReadTimeout=100;stream.WriteTimeout=100;buffer=new byte[d.GetMaxInputReportLength()];}
 public PadState Read(){int n=stream.Read(buffer,0,buffer.Length);return Processing.ParseDs4(buffer.AsSpan(0,n));}
 public void Rumble(byte large,byte small){lock(gate){var b=new byte[32];b[0]=5;b[1]=1;b[4]=small;b[5]=large;stream.Write(b);}}
 public void Dispose(){try{Rumble(0,0);}catch(IOException){}catch(ObjectDisposedException){}finally{stream.Dispose();}}
}
public sealed class XInputBackend(int slot):IInputBackend {
 [StructLayout(LayoutKind.Sequential)] public struct Gamepad{public ushort Buttons;public byte LT,RT;public short LX,LY,RX,RY;}
 [StructLayout(LayoutKind.Sequential)] public struct State{public uint Packet;public Gamepad Pad;}
 [StructLayout(LayoutKind.Sequential)] public struct Vibration{public ushort Large,Small;}
 [DllImport("xinput1_4.dll")] public static extern uint XInputGetState(uint index,out State state);
 [DllImport("xinput1_4.dll")] static extern uint XInputSetState(uint index,ref Vibration vibration);
 public PadState Read(){if(XInputGetState((uint)slot,out var s)!=0)throw new IOException("Контроллер отключён");static float N(short v)=>v<0?v/32768f:v/32767f;var p=s.Pad;return new((Buttons)p.Buttons,N(p.LX),N(p.LY),N(p.RX),N(p.RY),p.LT/255f,p.RT/255f);}
 public void Rumble(byte large,byte small){var v=new Vibration{Large=(ushort)(large*257),Small=(ushort)(small*257)};XInputSetState((uint)slot,ref v);}public void Dispose()=>Rumble(0,0);
}
public sealed class VigemBackend:IVirtualBackend {
 readonly ViGEmClient client;readonly IXbox360Controller pad;public int UserIndex=>pad.UserIndex;
 public VigemBackend(Action<byte,byte> feedback){client=new();try{pad=client.CreateXbox360Controller();pad.AutoSubmitReport=false;pad.FeedbackReceived+=(_,e)=>feedback(e.LargeMotor,e.SmallMotor);pad.Connect();}catch{client.Dispose();throw;}}
 public void Send(PadState s){pad.SetButtonsFull((ushort)s.Buttons);pad.LeftThumbX=(short)(s.LX*32767);pad.LeftThumbY=(short)(s.LY*32767);pad.RightThumbX=(short)(s.RX*32767);pad.RightThumbY=(short)(s.RY*32767);pad.LeftTrigger=(byte)(s.LT*255);pad.RightTrigger=(byte)(s.RT*255);pad.SubmitReport();}
 public void Dispose(){try{Send(new());pad.Disconnect();}finally{client.Dispose();}}
}
public static class Discovery {
 public static List<Device> Scan(int excludedSlot=-1){var list=new List<Device>();foreach(var h in DeviceList.Local.GetHidDevices())try{var usage=h.GetReportDescriptor().DeviceItems.Any(i=>i.Usages.GetAllValues().Any(u=>u is 0x10004 or 0x10005));if(!usage)continue;string name=h.GetProductName();list.Add(new(h.DevicePath,string.IsNullOrWhiteSpace(name)?"HID controller":name,$"HID • {h.VendorID:X4}:{h.ProductID:X4} • транспорт не определён",h));}catch{ /* Some unrelated HID interfaces deny metadata access. */ }
 for(int i=0;i<4;i++)if(i!=excludedSlot&&XInputBackend.XInputGetState((uint)i,out _)==0)list.Add(new("xinput:"+i,"XInput controller "+(i+1),"XInput • USB/Bluetooth не определяется API",Slot:i));return list;}
}

