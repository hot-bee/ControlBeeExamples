using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using PnpExample.Views;
using PnpExample.Actors.Head;
using PnpExample.Actors.Picker;
using PnpExample.Actors.Stage;
using PnpExample.Constants;
using PnpExample.Interfaces;
using PnpExample.Models;
using PnpExample.Services;
using PnpExample.ViewModels;
using ControlBee.Interfaces;
using ControlBee.Models;
using ControlBee.Services;
using ControlBee.Utils;
using ControlBee.Variables;
using ControlBeeWPF.Interfaces;
using ControlBeeWPF.Models;
using ControlBeeWPF.Services;
using ControlBeeWPF.ViewModels;
using ControlBeeWPF.Views;
using log4net;
using Microsoft.Extensions.DependencyInjection;
using PnpExample.Actors.Auxiliary;
using PnpExample.Actors.Syncer;
using ViewFactory = PnpExample.Services.ViewFactory;
using EventManager = ControlBee.Services.EventManager;
using SystemConfigurations = PnpExample.Models.SystemConfigurations;
using Application = System.Windows.Application;

namespace PnpExample;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly ILog Logger = LogManager.GetLogger("App");

    public App()
    {
        Startup += OnStartup;
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    private void OnStartup(object sender, StartupEventArgs e)
    {
        Logger.Info("Application is starting up.");
        TimeBeginPeriod(1);

        const string mutexName = @"Global\ControlBee.SingleInstance.{1FAD292E-B2DC-4207-AF76-61EA4F2A0D6F}";
        var mutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            Logger.Fatal("Another instance is running.");
            Shutdown();
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Directory.CreateDirectory("crashes");
            var logFile = $"crashes/crash_{DateTime.Now:yy-MM-dd_HHmmss}.log";
            File.WriteAllText(logFile, ((Exception)e.ExceptionObject).ToString());
            Process.Start("notepad.exe", logFile);
        };

        var loadingMainWindowView = new LoadingMainWindowView(null);
        loadingMainWindowView.Show();

        Task.Run(() =>
        {
            var systemConfigurations = new SystemConfigurations();
            systemConfigurations.Load();

            var services = new ServiceCollection();
            services.AddSingleton<IDatabase, SqliteDatabase>();
            services.AddSingleton<IVariableManager, VariableManager>();
            if (systemConfigurations.FakeMode)
                services.AddSingleton<ITimeManager, FrozenTimeManager>();
            else
                services.AddSingleton<ITimeManager, TimeManager>();
            services.AddSingleton<IPositionAxesMap, PositionAxesMap>();
            services.AddSingleton<IScenarioFlowTester, EmptyScenarioFlowTester>();
            services.AddSingleton<ISystemConfigurations, SystemConfigurations>();
            services.AddSingleton<IActorRegistry, ActorRegistry>();
            services.AddSingleton<ActorFactory>();
            services.AddSingleton<IAxisFactory, AxisFactory>();
            services.AddSingleton<IDeviceManager, DeviceManager>();
            services.AddSingleton<IDigitalInputFactory, DigitalInputFactory>();
            services.AddSingleton<IDigitalOutputFactory, DigitalOutputFactory>();
            services.AddSingleton<IAnalogInputFactory, AnalogInputFactory>();
            services.AddSingleton<IAnalogOutputFactory, AnalogOutputFactory>();
            services.AddSingleton<ISystemPropertiesDataSource, SystemPropertiesDataSource>();
            services.AddSingleton<IInitializeSequenceFactory, InitializeSequenceFactory>();
            services.AddSingleton<IBinaryActuatorFactory, BinaryActuatorFactory>();
            services.AddSingleton<IVisionFactory, VisionFactory>();
            services.AddSingleton<DialogDisplay>();
            services.AddSingleton<DialogViewFactory>();
            services.AddSingleton<DialogContextFactory>();
            services.AddSingleton<IDialogFactory, DialogFactory>();
            services.AddSingleton<IDeviceLoader, DeviceLoader>();

            services.AddSingleton<IViewFactory, ViewFactory>();
            services.AddTransient<HeaderView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<FrameViewModel>();
            services.AddTransient<FrameView>();
            services.AddSingleton<InitializationViewModel>();
            services.AddTransient<InitializationView>();
            services.AddTransient<ManualView>();
            services.AddTransient<FunctionView>();
            services.AddTransient<FunctionUnitView>();
            services.AddSingleton<FunctionUnitViewFactory>();
            services.AddTransient<SetupView>();
            services.AddSingleton<ActorMonitorViewModel>();
            services.AddTransient<ActorMonitorView>();
            services.AddTransient<AutoOperationView>();
            services.AddSingleton<AutoOperationViewModel>();
            services.AddSingleton<MachineTopViewModel>();
            services.AddTransient<ChangeableViewHolder>();
            services.AddTransient<DataMainView>();
            services.AddTransient<MonitorMainView>();
            services.AddSingleton<IDeviceMonitor, DeviceMonitor>();
            services.AddSingleton<AxesStatusView>();
            services.AddSingleton<AxesStatusViewModel>();
            services.AddSingleton<ActorItemExplorerViewFactory>();
            services.AddSingleton<ActorItemExplorerWholeView>();
            services.AddSingleton<TeachingViewFactory>();
            services.AddTransient<TeachingWholeView>();
            services.AddTransient<LogView>();
            services.AddTransient<LogViewModel>();
            services.AddTransient<ChangeRecipeView>();
            services.AddSingleton<IEventManager, EventManager>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddTransient<RecipeManagerView>();
            services.AddSingleton<RecipeManagerViewModel>();

            services.AddTransient<IDialog, PnpExampleDialog>();
            services.AddTransient<IDialogContext, PnpExampleDialogContext>();
            services.AddTransient<IDialogView, PnpExampleDialogView>();

            services.AddTransient<SyncerActor>();
            services.AddTransient<HeadActor>();
            services.AddTransient<PickerActor>();
            services.AddTransient<StageActor>();

            var serviceProvider = services.BuildServiceProvider();

            systemConfigurations = (SystemConfigurations)serviceProvider.GetRequiredService<ISystemConfigurations>();
            systemConfigurations.Load();

            var systemPropertiesDataSource =
                serviceProvider.GetRequiredService<ISystemPropertiesDataSource>();
            systemPropertiesDataSource.ReadFromFile();
            serviceProvider.GetRequiredService<IDeviceLoader>();

            var actorFactory = serviceProvider.GetRequiredService<ActorFactory>();
            HeadActor head;
            SyncerActor syncer;
            IActor sourceCameraHead = EmptyActor.Instance;
            IActor sourceStage0 = null;
            IActor lifter = EmptyActor.Instance;
            var ui = actorFactory.Create<UiActor>("Ui");
            var auxiliary = actorFactory.Create<AuxiliaryActor>("Auxiliary");
            syncer = actorFactory.Create<SyncerActor>("Syncer", StageType.Tray, StageType.Carrier);
            head = actorFactory.Create<HeadActor>("Head0", StageType.Tray, StageType.Carrier);
            sourceStage0 =
                actorFactory.Create<StageActor>("SourceStage0", StageType.Tray, TransferDirection.TransferOut, false);

            var targetStage =
                actorFactory.Create<StageActor>("TargetStage", StageType.Carrier, TransferDirection.TransferIn, false);
            var picker0 = actorFactory.Create<PickerActor>("Picker0");
            var picker1 = actorFactory.Create<PickerActor>("Picker1");

            var syncerPeers = new PeerContainer
            {
                Head = head,
                Picker0 = picker0,
                Picker1 = picker1,
                SourceStage0 = sourceStage0,
                TargetStage = targetStage,
            };
            auxiliary.SetPeers();
            syncer.SetPeers(syncerPeers);
            targetStage.SetPeers(syncer, head, null);
            head.SetPeers(syncer, sourceStage0, targetStage, picker0, picker1);
            picker0.SetPeers(syncer, head);
            picker1.SetPeers(syncer, head);

            ((StageActor)sourceStage0).SetPeers(syncer, head, null);

            var recipeName = systemConfigurations.RecipeName;
            var variableManager = serviceProvider.GetRequiredService<IVariableManager>();
            variableManager.Load(recipeName);

            auxiliary.Start();
            syncer.Start();
            targetStage.Start();
            head.Start();
            picker0.Start();

            picker1.Start();

            ((StageActor)sourceStage0).Start();

            var handler = new UiActorMessageHandler(Dispatcher);
            ui.SetHandler(handler);

            var functionUnitViewFactory = serviceProvider.GetRequiredService<FunctionUnitViewFactory>();

            var deviceMonitor = serviceProvider.GetRequiredService<IDeviceMonitor>();
            deviceMonitor.Start();

            var deviceManager = serviceProvider.GetRequiredService<IDeviceManager>();
            StartupUtils.LaunchInspectionBeeAndConnect("TcpVision", deviceManager);

            systemConfigurations.Version = "0.0.1";

            Dispatcher.Invoke(() =>
            {
                var mainView = serviceProvider.GetRequiredService<MainWindow>();
                _ = serviceProvider.GetRequiredService<ActorMonitorViewModel>();

                Current.MainWindow = mainView;
                mainView.Show();

                loadingMainWindowView.Close();
                loadingMainWindowView = null;
            });
        });
    }
}