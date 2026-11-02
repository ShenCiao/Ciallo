namespace Ciallo.Tool;

// Saved tool preferences store these as integers.
public enum LiquifyMode
{
    Push = 0,
    // Value 2 belonged to the former Pinch mode.
    Radial = 1,
    // Value 4 belonged to the former Thin mode.
    Thickness = 3,
    Pressure = 5,
}
