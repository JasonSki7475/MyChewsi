using InstallShield.Interop;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ChewsiPlugin.Setup.CustomActions
{
    [Target("MsiTarget")]
    internal sealed class MsiTarget : TargetWithLayout
    {
        private readonly int? _msiHandle;

        public MsiTarget(int msiHandle)
        {
            _msiHandle = msiHandle;
            Host = "localhost";
        }

        [RequiredParameter]
        public string Host { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            if (_msiHandle.HasValue)
            {
                var message = Layout.Render(logEvent);

                using (Msi.Install msi = Msi.CustomActionHandle(_msiHandle.Value))
                {
                    using (Msi.Record record = new Msi.Record(100))
                    {
                        record.SetString(0, "LOG: [1]");
                        record.SetString(1, message);
                        msi.ProcessMessage(Msi.InstallMessage.Info, record);
                    }
                }
            }
        }
    }
}
