using System.Text.Json;
using System.IO;
namespace Legacy;
[Flags] public enum Buttons : ushort { None=0,Up=1,Down=2,Left=4,Right=8,Start=16,Back=32,L3=64,R3=128,L1=256,R1=512,PS=1024,A=4096,B=8192,X=16384,Y=32768 }
public record PadState(Buttons Buttons=0,float LX=0,float LY=0,float RX=0,float RY=0,float LT=0,float RT=0);
public class Profile {
 public string Name {get;set;}="Default"; public double Deadzone {get;set;}=.12; public double Sensitivity {get;set;}=1; public bool InvertLY {get;set;} public bool InvertRY {get;set;} public bool SwapSticks {get;set;} public string Game {get;set;}=""; public bool SwapTriggers {get;set;} public double TriggerDeadzone {get;set;} public double TriggerSensitivity {get;set;}=1;
 public Dictionary<Buttons,Buttons> Mapping {get;set;}=Enum.GetValues<Buttons>().Where(b=>b!=0).ToDictionary(b=>b,b=>b);
 public void Validate(){if(string.IsNullOrWhiteSpace(Name)||Name.Length>80||!double.IsFinite(Deadzone)||Deadzone<0||Deadzone>.9||!double.IsFinite(Sensitivity)||Sensitivity<.1||Sensitivity>3||Game==null||!double.IsFinite(TriggerDeadzone)||TriggerDeadzone<0||TriggerDeadzone>.9||!double.IsFinite(TriggerSensitivity)||TriggerSensitivity<.1||TriggerSensitivity>3||Mapping==null||Mapping.Any(x=>!Enum.IsDefined(x.Key)||!Enum.IsDefined(x.Value)))throw new InvalidDataException("Некорректный профиль");}
}
public class Config { public int Version{get;set;}=1; public List<Profile> Profiles{get;set;}=[new()]; public int Active{get;set;} public bool AutoConnect{get;set;} public bool AutoProfile{get;set;} public int PollMs{get;set;}=8; public bool VirtualOutput{get;set;} }
public static class Processing {
 public static (float,float) Stick(float x,float y,double dz,double gain){var r=Math.Sqrt(x*x+y*y);if(r<=dz)return(0,0);var scale=Math.Min(1,(r-dz)/(1-dz)*gain)/r;return((float)(x*scale),(float)(y*scale));}
 public static PadState Apply(PadState s,Profile p){var l=Stick(s.LX,s.LY,p.Deadzone,p.Sensitivity);var r=Stick(s.RX,s.RY,p.Deadzone,p.Sensitivity);Buttons b=0;foreach(var pair in p.Mapping)if(pair.Key!=0&&s.Buttons.HasFlag(pair.Key))b|=pair.Value;if(p.InvertLY)l.Item2=-l.Item2;if(p.InvertRY)r.Item2=-r.Item2;if(p.SwapSticks)(l,r)=(r,l);float T(float v)=>(float)Math.Clamp((v-p.TriggerDeadzone)/(1-p.TriggerDeadzone)*p.TriggerSensitivity,0,1);var lt=T(s.LT);var rt=T(s.RT);if(p.SwapTriggers)(lt,rt)=(rt,lt);return new(b,l.Item1,l.Item2,r.Item1,r.Item2,lt,rt);}
 // DsHidMini driver/DsHid.c DS3_RAW_TO_DS4WINDOWS_HID_INPUT_REPORT, report ID 1.
 public static PadState ParseDs4(ReadOnlySpan<byte> d){if(d.Length<10||d[0]!=1)throw new InvalidDataException("Неподдерживаемый HID report");Buttons b=0;int h=d[5]&15;if(h is 7 or 0 or 1)b|=Buttons.Up;if(h is 1 or 2 or 3)b|=Buttons.Right;if(h is 3 or 4 or 5)b|=Buttons.Down;if(h is 5 or 6 or 7)b|=Buttons.Left;
 if((d[5]&16)!=0)b|=Buttons.X;if((d[5]&32)!=0)b|=Buttons.A;if((d[5]&64)!=0)b|=Buttons.B;if((d[5]&128)!=0)b|=Buttons.Y;
 Buttons[] bits=[Buttons.L1,Buttons.R1,0,0,Buttons.Back,Buttons.Start,Buttons.L3,Buttons.R3];for(int i=0;i<8;i++)if((d[6]&(1<<i))!=0)b|=bits[i];if((d[7]&1)!=0)b|=Buttons.PS;
 static float Axis(byte v)=>v>=128?(v-128)/127f:(v-128)/128f;
 return new(b,Axis(d[1]),-Axis(d[2]),Axis(d[3]),-Axis(d[4]),d[8]/255f,d[9]/255f);}
}
public sealed class ConfigStore {
 public string Folder{get;} public string? Warning{get;private set;} public ConfigStore(string? folder=null){Folder=folder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LegacyController");Directory.CreateDirectory(Folder);}
 public static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
 public static void Validate(Config c){if(c.Version!=1)throw new InvalidDataException("Версия конфигурации не поддерживается; оригинал сохранён в резервной копии");if(c.Profiles==null||c.Profiles.Count==0||c.Active<0||c.Active>=c.Profiles.Count||c.PollMs<4||c.PollMs>50)throw new InvalidDataException("Некорректная конфигурация");foreach(var p in c.Profiles){if(p==null)throw new InvalidDataException("Пустой профиль");p.Validate();}}
 public Config Load(){var path=Path.Combine(Folder,"config.json");if(!File.Exists(path))return new();try{var c=JsonSerializer.Deserialize<Config>(File.ReadAllText(path))??throw new InvalidDataException();Validate(c);return c;}catch(Exception e) when(e is JsonException or InvalidDataException){File.Copy(path,path+".backup-"+DateTime.UtcNow.Ticks);Warning=e.Message;return new();}}
 public void Save(Config c){Validate(c);var path=Path.Combine(Folder,"config.json");var tmp=path+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(c,Json));File.Move(tmp,path,true);}
}

