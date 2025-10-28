using ControlBee.Interfaces;
using ControlBee.Models;
using PnpExample.Interfaces;

namespace PnpExample.Actors.Syncer;

public class PeerContainer : IPeerContainer
{
    public IActor Head = EmptyActor.Instance;
    public IActor Picker0 = EmptyActor.Instance;
    public IActor Picker1 = EmptyActor.Instance;
    public IActor SourceStage0 = EmptyActor.Instance;
    public IActor TargetStage = EmptyActor.Instance;

    public IActor[] ToArray()
    {
        var list = new List<IActor>();
        GetType()
            .GetFields()
            .Where(x => typeof(IActor).IsAssignableFrom(x.FieldType))
            .Select(x => x.GetValue(this))
            .ToList()
            .ForEach(x => list.Add((IActor)x!));
        return list.Where(x => x != EmptyActor.Instance).ToArray();
    }
}