using Godot;
using Steamworks;
using System;

namespace Ciallo;

public partial class SteamManager : Node
{
    public const uint AppId = 4103990;

    public override void _EnterTree()
    {
        try
        {
            SteamClient.Init(AppId, true);
            if (SteamClient.IsValid)
                GD.Print("Steam initialized successfully.");
            else
                throw new Exception();
        }
        catch (Exception e)
        {
            GD.Print($"Steam initialization failed: {e.Message}");
            return;
        }
    }

    public override void _ExitTree()
    {
        SteamClient.Shutdown();
    }
}
