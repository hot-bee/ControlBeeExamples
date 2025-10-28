using System.Windows.Controls;
using PnpExample.Views;
using ControlBee.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using PnpExample.Interfaces;
using PnpExample.Views;
using UserControl = System.Windows.Controls.UserControl;

namespace PnpExample.Services;

public class FunctionUnitViewFactory(IServiceProvider serviceProvider, IActorRegistry actorRegistry)
{
    private readonly Dictionary<string, Type> _customViews = new();

    public UserControl Create(IActor actor)
    {
        var view = new FunctionUnitView(actorRegistry, actor);
        if (!_customViews.TryGetValue(actor.Name, out var viewType))
            return view;
        var customView = serviceProvider.GetRequiredService(viewType);
        if (customView is IInitWithActorName initWithActorName)
            initWithActorName.Init(actor.Name);
        view.SetCustomView((UserControl)customView);
        return view;
    }

    public void AddCustomView(string actorName, Type viewType)
    {
        _customViews[actorName] = viewType;
    }
}
