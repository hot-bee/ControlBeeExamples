using ControlBee.Utils;
using ControlBee.Variables;

namespace PnpExample.Models;

public class Product : PropertyVariable
{
    private bool _exists;
    private int _pickupPickerIndex;
    private int _placePickerIndex;
    private int _pickupCol;
    private int _pickupRow;
    private int _placeCol;
    private int _placeRow;
    private double _pickupPosX;
    private double _pickupPosY;
    private double _placePosX;
    private double _placePosY;
    private int _pickupStageIndex;
    public Guid Guid = Guid.NewGuid();

    public VisionResult UlcVisionBlobResult { get; set; } = new();
    public VisionResult UlcVisionMatchResult { get; set; } = new();
    public VisionResult SourceVisionResult { get; set; } = new();
    public VisionResult TargetVisionResult { get; set; } = new();
    public VisionResult SideVisionResult { get; set; } = new();
    public bool VisionNg => UlcVisionMatchResult.Ng || SideVisionResult.Ng || UlcVisionBlobResult.Ng;

    public bool Exists
    {
        get => _exists;
        set => ValueChangedUtils.SetField(ref _exists, value, OnValueChanging, OnValueChanged);
    }

    public int PickupStageIndex
    {
        get => _pickupStageIndex;
        set => ValueChangedUtils.SetField(ref _pickupStageIndex, value, OnValueChanging, OnValueChanged);
    }

    public int PickupRow
    {
        get => _pickupRow;
        set => ValueChangedUtils.SetField(ref _pickupRow, value, OnValueChanging, OnValueChanged);
    }
    public int PlaceCol
    {
        get => _placeCol;
        set => ValueChangedUtils.SetField(ref _placeCol, value, OnValueChanging, OnValueChanged);
    }

    public int PlaceRow
    {
        get => _placeRow;
        set => ValueChangedUtils.SetField(ref _placeRow, value, OnValueChanging, OnValueChanged);
    }

    public int PickupPickerIndex
    {
        get => _pickupPickerIndex;
        set => ValueChangedUtils.SetField(ref _pickupPickerIndex, value, OnValueChanging, OnValueChanged);
    }
    
    public int PlacePickerIndex
    {
        get => _placePickerIndex;
        set => ValueChangedUtils.SetField(ref _placePickerIndex, value, OnValueChanging, OnValueChanged);
    }
    
    public int PickupCol
    {
        get => _pickupCol;
        set => ValueChangedUtils.SetField(ref _pickupCol, value, OnValueChanging, OnValueChanged);
    }

    public double PickupPosX
    {
        get => _pickupPosX;
        set => ValueChangedUtils.SetField(ref _pickupPosX, value, OnValueChanging, OnValueChanged);
    }

    public double PickupPosY
    {
        get => _pickupPosY;
        set => ValueChangedUtils.SetField(ref _pickupPosY, value, OnValueChanging, OnValueChanged);
    }
    
    public double PlacePosX
    {
        get => _placePosX;
        set => ValueChangedUtils.SetField(ref _placePosX, value, OnValueChanging, OnValueChanged);
    }

    public double PlacePosY
    {
        get => _placePosY;
        set => ValueChangedUtils.SetField(ref _placePosY, value, OnValueChanging, OnValueChanged);
    }

    public override void OnDeserialized()
    {
        // Empty
    }
}