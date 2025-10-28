using System.Globalization;

namespace PnpExample.Models;

public class VisionResult
{
    public bool Inspecting { get; set; }
    public bool Inspected { get; set; }
    public bool NeedInspect => !Inspecting && !Inspected;
    public bool Ng => Inspected && !Ok;
    public bool Ok { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Angle { get; set; }
    public double[]? EdgeLengths { get; set; }

    public string EdgeLengthsString()
    {
        return EdgeLengths == null
            ? ""
            : string.Join("/", EdgeLengths.ToList().ConvertAll(x => x.ToString("0.00", CultureInfo.InvariantCulture)));
    }
}