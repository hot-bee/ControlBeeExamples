using System.Text.Json.Serialization;
using ControlBee.Utils;
using ControlBee.Variables;

namespace PnpExample.Models;

public class GridContainer : PropertyVariable, ICloneable, IDisposable
{
    private bool _exists;

    public GridContainer() { }

    public GridContainer(int size1, int size2, bool initialValue)
    {
        CellExists = new Array2D<bool>(size1, size2);
        WorkDone = new Array2D<bool>(size1, size2);
        Consumers = new Array2D<int>(size1, size2);
        CellVisionResult = new Array2D<VisionResult>(size1, size2);
        CellPostVisionResult = new Array2D<VisionResult>(size1, size2);
        for (var i = 0; i < size1; i++)
        for (var j = 0; j < size2; j++)
        {
            CellExists[i, j] = initialValue;
            WorkDone[i, j] = false;
            Consumers[i, j] = -1;
            CellVisionResult[i, j] = new VisionResult();
            CellPostVisionResult[i, j] = new VisionResult();
        }
        CellExists.ValueChanged += CellExistsOnValueChanged;
        WorkDone.ValueChanged += WorkDoneOnValueChanged;
        Consumers.ValueChanged += ConsumersOnValueChanged;
        CellVisionResult.ValueChanged += CellVisionResultOnValueChanged;
        CellPostVisionResult.ValueChanged += CellPostVisionResultOnValueChanged;
        
    }

    private void WorkDoneOnValueChanged(object? sender, ValueChangedArgs e)
    {
        OnValueChanged(
            new ValueChangedArgs(
                ((object[])[nameof(WorkDone)]).Concat(e.Location).ToArray(),
                e.OldValue,
                e.NewValue
            )
        );
    }

    private void ConsumersOnValueChanged(object? sender, ValueChangedArgs e)
    {
        OnValueChanged(
            new ValueChangedArgs(
                ((object[])[nameof(Consumers)]).Concat(e.Location).ToArray(),
                e.OldValue,
                e.NewValue
            )
        );
    }

    private void CellPostVisionResultOnValueChanged(object? sender, ValueChangedArgs e)
    {
        OnValueChanged(
            new ValueChangedArgs(
                ((object[])[nameof(CellPostVisionResult)]).Concat(e.Location).ToArray(),
                e.OldValue,
                e.NewValue
            )
        );
    }

    private void CellVisionResultOnValueChanged(object? sender, ValueChangedArgs e)
    {
        OnValueChanged(
            new ValueChangedArgs(
                ((object[])[nameof(CellVisionResult)]).Concat(e.Location).ToArray(),
                e.OldValue,
                e.NewValue
            )
        );
    }

    public GridContainer(bool exists, Array2D<bool> cellExists)
    {
        _exists = exists;
        CellExists = cellExists;
    }

    [JsonIgnore]
    public (int, int) Size => (CellExists.Size.Item1, CellExists.Size.Item2);

    public bool Exists
    {
        get => _exists;
        set => ValueChangedUtils.SetField(ref _exists, value, OnValueChanging, OnValueChanged);
    }

    public Array2D<bool> CellExists { get; set; } = new(0, 0);
    public Array2D<VisionResult> CellVisionResult { get; set; } = new(0, 0);
    public Array2D<VisionResult> CellPostVisionResult { get; set; } = new(0, 0);
    public Array2D<bool> WorkDone { get; set; } = new(0, 0);
    public Array2D<int> Consumers { get; set; } = new(0, 0);

    public void CloneAddedArray()
    {
        var (item1, item2) = Size;
        if (CellPostVisionResult.Size.Item1 != item1 ||
            CellPostVisionResult.Size.Item2 != item2)
        {
            CellPostVisionResult = new Array2D<VisionResult>(item1, item2);
            for (var i = 0; i < item1; i++)
            for (var j = 0; j < item2; j++)
                CellPostVisionResult[i, j] = new VisionResult();
        }
    }

    public object Clone()
    {
        var cloneGridContainer = new GridContainer(Exists, (Array2D<bool>)CellExists.Clone())
        {
            CellVisionResult = (Array2D<VisionResult>)CellVisionResult.Clone(),
            CellPostVisionResult = (Array2D<VisionResult>)CellPostVisionResult.Clone(),
            WorkDone = (Array2D<bool>)WorkDone.Clone(),
            Consumers = (Array2D<int>)Consumers.Clone(),
        };
        cloneGridContainer.CloneAddedArray();
        return cloneGridContainer;
    }

    public void Dispose()
    {
        CellExists.ValueChanged -= CellExistsOnValueChanged;
        WorkDone.ValueChanged -= WorkDoneOnValueChanged;
        CellVisionResult.ValueChanged -= CellVisionResultOnValueChanged;
        CellPostVisionResult.ValueChanged -= CellPostVisionResultOnValueChanged;
        Consumers.ValueChanged -= ConsumersOnValueChanged;
    }

    private void CellExistsOnValueChanged(object? sender, ValueChangedArgs e)
    {
        OnValueChanged(
            new ValueChangedArgs(
                ((object[])[nameof(CellExists)]).Concat(e.Location).ToArray(),
                e.OldValue,
                e.NewValue
            )
        );
    }

    public override void OnDeserialized()
    {
        CellExists.OnDeserialized();
        CellExists.ValueChanged += CellExistsOnValueChanged;
        WorkDone.ValueChanged += WorkDoneOnValueChanged;
        Consumers.ValueChanged += ConsumersOnValueChanged;
        CellVisionResult.ValueChanged += CellVisionResultOnValueChanged;
        CellPostVisionResult.ValueChanged += CellPostVisionResultOnValueChanged;
    }
}
