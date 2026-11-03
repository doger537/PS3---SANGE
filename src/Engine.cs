using Microsoft.Extensions.Logging;
using System.IO;
namespace Legacy;
public sealed class Engine(ILogger<Engine> log):IAsyncDisposable {
 IInputBackend? input;IVirtualBackend? output;CancellationTokenSource? cts;Task? worker;public PadState Raw{get;private set;}=new();public PadState Processed{get;private set;}=new();public string Status{get;private set;}="Не подключено";public int VirtualSlot=>output?.UserIndex??-1;
 public async Task Connect(Device device,Func<Profile> profile,Config config,bool hideConfirmed){await Disconnect();if(config.VirtualOutput&&(device.Hid?.VendorID!=0x7331||!hideConfirmed))throw new InvalidOperationException("Виртуальный выход доступен для DsHidMini DS4Windows mode. Сначала настройте HidHide, добавьте Legacy.exe в разрешённые приложения и скройте физический DS3; затем подтвердите настройку в Legacy. XInput → XInput отключён для предотвращения обратной связи.");
 var virtualEnabled=config.VirtualOutput;var pollMs=config.PollMs;cts=new();var token=cts.Token;Status="Подключение…";
 worker=Task.Run(async()=>{while(!token.IsCancellationRequested){try{input=device.Hid!=null?new Ds3Backend(device.Hid):new XInputBackend(device.Slot);if(virtualEnabled)output=new VigemBackend((l,s)=>{try{input?.Rumble(l,s);}catch(Exception e){log.LogWarning(e,"Rumble failed");}});Status="Подключено • "+(output==null?"просмотр ввода":"виртуальный Xbox активен");log.LogInformation("Connected {Name}",device.Name);while(!token.IsCancellationRequested){try{Raw=input.Read();Processed=Processing.Apply(Raw,profile());output?.Send(Processed);}catch(TimeoutException){Raw=new();Processed=new();output?.Send(new());}await Task.Delay(pollMs,token);}}
 catch(OperationCanceledException) when(token.IsCancellationRequested){break;}catch(Exception e){Status="Ожидание устройства: "+e.Message;log.LogWarning(e,"Input/output disconnected");}
 finally{try{output?.Dispose();}catch(Exception e){log.LogWarning(e,"Output cleanup");}output=null;try{input?.Dispose();}catch(Exception e){log.LogWarning(e,"Input cleanup");}input=null;Raw=new();Processed=new();}
 try{await Task.Delay(2000,token);}catch(OperationCanceledException){break;}}
 });}
 public async Task Disconnect(){if(cts!=null){cts.Cancel();if(worker!=null)await worker;cts.Dispose();cts=null;}Status="Не подключено";}
 public async Task TestRumble(){var target=input??throw new InvalidOperationException("Подключите контроллер");target.Rumble(140,100);try{await Task.Delay(350);}finally{try{target.Rumble(0,0);}catch(Exception e){log.LogWarning(e,"Rumble stop");}}}
 public async ValueTask DisposeAsync()=>await Disconnect();
}
public sealed class FileLogProvider(string folder):ILoggerProvider {
 readonly object gate=new();public ILogger CreateLogger(string category)=>new Writer(this,category);public void Dispose(){}void Write(string text){lock(gate){var path=Path.Combine(folder,"legacy.log");if(File.Exists(path)&&new FileInfo(path).Length>2_000_000)File.Move(path,path+".1",true);File.AppendAllText(path,text+Environment.NewLine);}}
 sealed class Writer(FileLogProvider owner,string category):ILogger{public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;public bool IsEnabled(LogLevel level)=>level>=LogLevel.Information;public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> format){if(IsEnabled(level))owner.Write($"{DateTimeOffset.Now:O} {level} {category}: {format(state,error)} {error}");}}
}

