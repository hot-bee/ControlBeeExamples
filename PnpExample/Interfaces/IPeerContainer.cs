using ControlBee.Interfaces;

namespace PnpExample.Interfaces;

public interface IPeerContainer
{
    IActor[] ToArray();
}