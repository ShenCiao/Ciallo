namespace Ciallo.Tool;

public enum LiquifyMode
{
    Push,
    Expand,
    Pinch,
    Thickness,
    // Value 4 belonged to the former Thin mode in saved tool preferences.
    Pressure = 5,
}
