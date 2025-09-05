global using static Ciallo.Data.Global;
using Ciallo.Tool;

namespace Ciallo.Data;

public static partial class Global
{
    public static readonly Preference Preferences = new();
    public static readonly ToolManager ToolManager = new();
}