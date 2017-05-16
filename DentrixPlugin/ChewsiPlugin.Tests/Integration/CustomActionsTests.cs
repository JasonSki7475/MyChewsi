using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.Setup.CustomActions;
using NUnit.Framework;

namespace ChewsiPlugin.Tests.Integration
{
    [TestFixture(Ignore = "Manual")]
    public class CustomActionsTests
    {
        [Test]
        public void SetCurrentPMS_OpenDental()
        {
            // Arrange
            var repository = new Repository();

            // Act
            CustomActions.Setup("OpenDental", "c:\\", "True");

            // Assert
            var path = repository.GetSettingValue<string>(Settings.PMS.PathKey);
            Assert.AreEqual("C:\\Program Files (x86)\\Open Dental", path);
            var type = repository.GetSettingValue<Settings.PMS.Types>(Settings.PMS.TypeKey);
            Assert.AreEqual(Settings.PMS.Types.OpenDental, type);
            var isClient = repository.GetSettingValue<bool>(Settings.IsClient);
            Assert.IsTrue(isClient);
        }
    }
}
