using PnpExample.Views;
using ControlBee.Interfaces;
using ControlBeeAbstract.Exceptions;
using ControlBeeWPF.Services;
using ControlBeeWPF.ViewModels;
using ControlBeeWPF.Views;
using Microsoft.Extensions.DependencyInjection;
using TeachingView = PnpExample.Views.TeachingView;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Services;

public class ViewFactory(IServiceProvider serviceProvider) : ControlBeeWPF.Services.ViewFactory(serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public override UserControl Create(Type viewType, params object?[]? args)
    {
        try
        {
            return base.Create(viewType, args);
        }
        catch (ValueError)
        {
            if (viewType == typeof(FunctionView))
            {
                var actorRegistry = _serviceProvider.GetRequiredService<IActorRegistry>();
                var functionViewFactory = serviceProvider.GetRequiredService<FunctionUnitViewFactory>();
                var view = new FunctionView(actorRegistry, this, functionViewFactory);

                return view;
            }

            if (viewType == typeof(VisionStatusView))
            {
                var visionDeviceName = (string)args![0]!;
                var deviceManager = _serviceProvider.GetRequiredService<IDeviceManager>();
                var viewModel = new VisionStatusViewModel(visionDeviceName, deviceManager);
                var view = new VisionStatusView(viewModel);
                return view;
            }

            if (viewType == typeof(TeachingView))
            {
                var actorName = (string)args![0]!;
                var actorRegistry = _serviceProvider.GetRequiredService<IActorRegistry>();
                var teachingViewFactory = _serviceProvider.GetRequiredService<TeachingViewFactory>();
                var viewModel = new TeachingViewModel(actorName, actorRegistry);
                var view = new TeachingView(
                    actorName,
                    viewModel,
                    teachingViewFactory,
                    actorRegistry,
                    this
                );
                return view;
            }

            return (UserControl)_serviceProvider.GetRequiredService(viewType);
        }

        throw new ValueError();
    }
}