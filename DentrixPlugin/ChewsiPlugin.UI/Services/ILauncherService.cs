namespace ChewsiPlugin.UI.Services
{
    internal interface ILauncherService
    {
        bool GetLauncherStartup();
        void StartLauncher();
        void KillLauncher();
        void SetLauncherStartup(bool startLauncher);
        void StartPms(string pmsExecutablePath);
    }
}
