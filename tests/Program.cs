using Legacy;
using System.IO;
using System.Text.Json;
int count=0;
void Test(string name,Action run){run();count++;Console.WriteLine("PASS "+name);}
void Check(bool ok){if(!ok)throw new Exception("Assertion failed");}
void Reject(Action a){try{a();}catch(InvalidDataException){return;}throw new Exception("Expected validation failure");}
Test("Deadzone center",()=>Check(Processing.Stick(.01f,.02f,.12,1)==(0,0)));
Test("Deadzone boundary",()=>Check(Math.Abs(Processing.Stick(.1f,0,.1,1).Item1)<.000001));
Test("Radial clamp",()=>{var(x,y)=Processing.Stick(1,1,.1,3);Check(Math.Abs(x*x+y*y-1)<.0001);});
Test("Mapping and inversion",()=>{var p=new Profile{InvertLY=true};p.Mapping[Buttons.A]=Buttons.B;var s=Processing.Apply(new(Buttons.A,0,1),p);Check(s.Buttons==Buttons.B&&s.LY==-1);});
Test("Stick swap",()=>Check(Processing.Apply(new(LX:1),new(){SwapSticks=true}).RX==1));
Test("Invalid profile",()=>Reject(()=>new Profile{Deadzone=double.NaN}.Validate()));
Test("Unknown config version",()=>Reject(()=>ConfigStore.Validate(new(){Version=2})));
Test("DS3 DS4Windows neutral",()=>{byte[] b=[1,128,128,128,128,8,0,0,0,0];Check(Processing.ParseDs4(b)==new PadState());});
Test("DS3 axes buttons triggers",()=>{byte[] b=[1,255,0,0,255,0x21,0x31,1,255,128];var s=Processing.ParseDs4(b);Check(s.LX==1&&s.LY==1&&s.RX==-1&&s.RY==-1&&s.LT==1&&s.Buttons.HasFlag(Buttons.A|Buttons.Up|Buttons.Right|Buttons.L1|Buttons.PS|Buttons.Start|Buttons.Back));});
Test("Invalid report",()=>Reject(()=>Processing.ParseDs4([1,0])));
var folder=Path.Combine(Path.GetTempPath(),"LegacyTests-"+Guid.NewGuid());var store=new ConfigStore(folder);
Test("Config roundtrip",()=>{var c=new Config();c.Profiles[0].Name="Игра";store.Save(c);Check(store.Load().Profiles[0].Name=="Игра");});
Test("Corrupt config backup",()=>{File.WriteAllText(Path.Combine(folder,"config.json"),"{broken");Check(store.Load().Profiles.Count==1&&store.Warning!=null&&Directory.GetFiles(folder,"*.backup-*").Length==1);});
Test("Mapping export roundtrip",()=>{var p=new Profile();p.Mapping[Buttons.X]=Buttons.Y;var q=JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p))!;Check(q.Mapping[Buttons.X]==Buttons.Y);});
Test("Trigger processing",()=>{var s=Processing.Apply(new(LT:.05f,RT:1),new(){TriggerDeadzone=.1,SwapTriggers=true});Check(s.LT==1&&s.RT==0);}); Test("Null profile rejected",()=>Reject(()=>ConfigStore.Validate(new(){Profiles=[null!]}))); Console.WriteLine($"{count} tests passed; no hardware tests performed.");


