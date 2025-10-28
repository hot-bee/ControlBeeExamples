using System.Globalization;
using System.IO;
using System.Text;
using PnpExample.Models;
using log4net;

namespace PnpExample.Services;

public class ProductionLogger
{
    private static readonly ILog Logger = LogManager.GetLogger("ProductionLogWriter");
    private static readonly List<string> StaticColumnHeaders = ["DateTime", "Guid"];

    private readonly string _baseDir;
    private readonly List<string> _logHeaders;
    private readonly Encoding _utf8WithBom = new UTF8Encoding(true);
    private string _currentDate = string.Empty;
    private string _filePath = string.Empty;

    public ProductionLogger(List<string> logHeaders, string baseDir = "production_logs")
    {
        _logHeaders = logHeaders;
        _baseDir = baseDir;
        CreateWorkLogFile();
    }

    private void CreateWorkLogFile()
    {
        if (DateTime.Today.ToString("yyyy-MM-dd") == _currentDate) return;

        Directory.CreateDirectory(_baseDir);
        _currentDate = DateTime.Today.ToString("yyyy-MM-dd");
        _filePath = Path.Combine(_baseDir, _currentDate + ".csv");
        if (File.Exists(_filePath)) return;

        List<string> columnItems = [];
        columnItems.AddRange(StaticColumnHeaders);
        columnItems.AddRange(_logHeaders);
        Write(string.Join(",", columnItems));
    }

    private void Write(string content)
    {
        CreateWorkLogFile();

        try
        {
            using var writer = new StreamWriter(_filePath, true, _utf8WithBom);
            writer.WriteLine(content);
        }
        catch (Exception e)
        {
            Logger.Error($"WorkLog write failed reason why: {e.Message}");
        }
    }

    private string Parse(Product product, string header)
    {
        var ret = header switch
        {
            nameof(product.UlcVisionMatchResult) => product.UlcVisionMatchResult.Ok.ToString(),
            nameof(product.PickupStageIndex) => product.PickupStageIndex.ToString(),
            nameof(product.PickupPickerIndex) => product.PickupPickerIndex.ToString(),
            nameof(product.PickupRow)=> product.PickupRow.ToString(),
            nameof(product.PickupCol) => product.PickupCol.ToString(),
            nameof(product.PickupPosX) => product.PickupPosX.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.PickupPosY) => product.PickupPosY.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.SourceVisionResult) + "X" => product.SourceVisionResult.X.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.SourceVisionResult) + "Y" => product.SourceVisionResult.Y.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.SourceVisionResult) + "Angle" => product.SourceVisionResult.Angle.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.SourceVisionResult) + "Lengths" => product.SourceVisionResult.EdgeLengthsString(),
            nameof(product.PlacePickerIndex) => product.PlacePickerIndex.ToString(),
            nameof(product.PlaceRow) => product.PlaceRow.ToString(),
            nameof(product.PlaceCol) => product.PlaceCol.ToString(),
            nameof(product.PlacePosX) => product.PlacePosX.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.PlacePosY) => product.PlacePosY.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.UlcVisionMatchResult) + "X" => product.UlcVisionMatchResult.X.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.UlcVisionMatchResult) + "Y" => product.UlcVisionMatchResult.Y.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.UlcVisionMatchResult) + "Angle" => product.UlcVisionMatchResult.Angle.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.UlcVisionMatchResult) + "Lengths" => product.UlcVisionMatchResult.EdgeLengthsString(),
            nameof(product.TargetVisionResult) + "X" => product.TargetVisionResult.X.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.TargetVisionResult) + "Y" => product.TargetVisionResult.Y.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.TargetVisionResult) + "Angle" => product.TargetVisionResult.Angle.ToString("0.00", CultureInfo.InvariantCulture),
            nameof(product.TargetVisionResult) + "Lengths" => product.TargetVisionResult.EdgeLengthsString(),
            _ => string.Empty
        };

        return ret + ",";
    }

    public void WriteProduct(Product? product)
    {
        if (product is null) return;

        var res = DateTime.Now.ToString("HH:mm:ss") + ",";
        res += product.Guid + ",";
        res += _logHeaders.Aggregate("", (current, header) => current += Parse(product, header));

        Write(res);
    }
}