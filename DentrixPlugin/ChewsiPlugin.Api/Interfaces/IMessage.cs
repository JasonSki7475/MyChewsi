namespace ChewsiPlugin.Api.Interfaces
{
    public interface IMessage
    {
        string Header { get; }
        string Text { get; }
        string ButtonText { get; }
    }
}