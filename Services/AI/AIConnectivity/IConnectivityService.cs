using System;

namespace ProGlassAutomation.Services.AI.AIConnectivity
{
    public interface IConnectivityService
    {
        bool IsOnline { get; }
        event EventHandler<bool> ConnectivityChanged;
        void StartMonitoring();
        void StopMonitoring();
    }
}