namespace ChewsiPlugin.Api
{
    public interface ISettings
    {
        string DatabaseFilePath { get; }
        bool Initialized();
        void Initialize();
    }
}