using System;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Repository;
using NUnit.Framework;

namespace ChewsiPlugin.Tests.Integration
{
    [TestFixture]
    public class ChewsiApiTests
    {
        [Test]
        public void RegisterPlugin_ShouldReturnMachindeId()
        {
            // Arrange
            var api = new ChewsiApi();
            var request = new RegisterPluginRequest("002480857", "1368 BEACON ST NO 105", "", Settings.PMS.Types.Dentrix, "6.2");

            // Act
            var response = api.RegisterPlugin(request);
            
            // Assert
            Guid guid;
            Assert.IsTrue(Guid.TryParse(response, out guid), "Machine Id should be GUID");
        }

        [Test]
        public void RegisterPluginWithWrongParameters_ShouldReturnMachindeId()
        {
            // Arrange
            var api = new ChewsiApi();
            var request = new RegisterPluginRequest("123456789", "Test", "", Settings.PMS.Types.Dentrix, "6.2");

            // Act
            var response = api.RegisterPlugin(request);

            // Assert
            Guid guid;
            Assert.IsTrue(Guid.TryParse(response, out guid), "Machine Id should be GUID");
        }

        ProviderInformation GetValidProvider()
        {
            return new ProviderInformation
            {
                NPI = "1013101740",
                TIN = "002480857"
            };
        }

        SubscriberInformation GetValidSubscriber()
        {
            return new SubscriberInformation
            {
                Id = "0000000004",
                SubscriberDateOfBirth = DateTime.Parse("09/01/1986"),
                SubscriberFirstName = "Jessica",
                SubscriberLastName = "THEZZZ"
            };
        }


        private ProviderAddressInformation GetValidProviderAddress()
        {
            return new ProviderAddressInformation
            {
                RenderingAddress = "1368 BEACON ST NO 105",
                RenderingCity = "BROOKLINE",
                RenderingState = "MA",
                RenderingZip = "02446"
            };
        }

        [Test]
        public void ValidateSubscriberAndProvider_ShouldReturnValidStatus()
        {
            // Arrange
            var api = new ChewsiApi();
            ProviderInformation provider = GetValidProvider();
            SubscriberInformation subscriber = GetValidSubscriber();
            ProviderAddressInformation providerAddress = GetValidProviderAddress();

            // Act
            ValidateSubscriberAndProviderResponse response = api.ValidateSubscriberAndProvider(provider, providerAddress, subscriber);
            
            // Assert
            // Validate provider
            Assert.IsNull(response.ProviderID);
            Assert.IsNull(response.ProviderValidationMessage);
            Assert.AreEqual("Valid", response.ProviderValidationStatus);
            // Validate subscriber
            Assert.AreEqual("4", response.SubscriberID);
            Assert.IsNull(response.SubscriberValidationMessage);
            Assert.AreEqual("Valid", response.SubscriberValidationStatus);

            //TODO
            Assert.AreEqual("", response.OfficeNumber);

            Assert.AreEqual(true, response.ValidationPassed);
        }

        [Test]
        public void ValidateSubscriberAndProvider_WhenProviderDataIsIncorrect_ShouldReturnProviderNotFoundStatus()
        {
            // Arrange
            var api = new ChewsiApi();
            ProviderInformation provider = GetValidProvider();
            SubscriberInformation subscriber = GetValidSubscriber();
            ProviderAddressInformation providerAddress = GetValidProviderAddress();

            // Act
            ValidateSubscriberAndProviderResponse response = api.ValidateSubscriberAndProvider(provider, providerAddress, subscriber);
            
            // Assert
            // Validate provider
            Assert.IsNull(response.ProviderID);
            Assert.AreEqual("We did not find the provider for which this claim is being submitted for in the list of Chewsi registered providers. Please validate that the claim is being submitted under a provider who has already registered themselves under the Chewsi network.", 
                response.ProviderValidationMessage);
            Assert.AreEqual("Provider Not Found", response.ProviderValidationStatus);
            // Validate subscriber
            Assert.AreEqual("4", response.SubscriberID);
            Assert.IsNull(response.SubscriberValidationMessage);
            Assert.AreEqual("Valid", response.SubscriberValidationStatus);

            //TODO
            Assert.AreEqual("", response.OfficeNumber);

            Assert.AreEqual(false, response.ValidationPassed);
        }

        [Test]
        public void ValidateSubscriberAndProvider_WhenSubscriberDataIsIncorrect_ShouldReturnSubscriberNotFoundStatus()
        {
            // Arrange
            var api = new ChewsiApi();
            ProviderInformation provider = GetValidProvider();
            SubscriberInformation subscriber = GetValidSubscriber();
            subscriber.Id = "123456"; // API checks First Name and Chewsi Id
            ProviderAddressInformation providerAddress = GetValidProviderAddress();

            // Act
            ValidateSubscriberAndProviderResponse response = api.ValidateSubscriberAndProvider(provider, providerAddress, subscriber);

            // Assert
            // Validate provider
            Assert.IsNull(response.ProviderID);
            Assert.IsNull(response.ProviderValidationMessage);
            Assert.AreEqual("Valid", response.ProviderValidationStatus);
            // Validate subscriber
            Assert.IsNull(response.SubscriberID);
            Assert.AreEqual("Please validate that the subscriber's Chewsi ID and First Name match the information shown before proceeding. If the information does not match, the Chewsi ID may have been keyed into the practice management system incorrectly. Please ask the subscriber for their Chewsi ID to validate.", 
                response.SubscriberValidationMessage);
            Assert.AreEqual("Subscriber Not Found", response.SubscriberValidationStatus);

            //TODO
            Assert.AreEqual("", response.OfficeNumber);

            Assert.AreEqual(false, response.ValidationPassed);
        }
    }
}
