using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Legacy;
public partial class App:Application {
 ServiceProvider? services;
 protected override void OnStartup(StartupEventArgs e){base.OnStartup(e);var store=new ConfigStore();services=new ServiceCollection().AddSingleton(store).AddLogging(b=>b.AddProvider(new FileLogProvider(store.Folder))).AddSingleton<Engine>().AddSingleton<MainWindow>().BuildServiceProvider();DispatcherUnhandledException+=(_,a)=>{MessageBox.Show(a.Exception.Message,"Legacy • ошибка");a.Handled=true;};services.GetRequiredService<MainWindow>().Show();}
 protected override async void OnExit(ExitEventArgs e){if(services!=null)await services.DisposeAsync();base.OnExit(e);}
}
