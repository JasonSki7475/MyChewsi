using System;
using System.Collections.Generic;
using System.Linq;
using ChewsiPlugin.Api;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.Services;
using ChewsiPlugin.UI.ViewModels;
using Moq;
using NUnit.Framework;
using Appointment = ChewsiPlugin.Api.Common.Appointment;

namespace ChewsiPlugin.Tests.Unit
{   
    [TestFixture]
    public class AppServiceTests
    {
        private Mock<IDialogService> _dialogServiceMock;
        private bool _loadingIndicatorShown;
        private Mock<IChewsiApi> _apiMock;
        private List<Appointment> _appointmentsPms;
        private Mock<ISettingsViewModel> _settingsViewModelMock;
        private Mock<IRepository> _repositoryMock;
        private Mock<IDentalApiFactoryService> _dentalApiFactoryService;
        private Mock<IDentalApi> _dentalApiMock;
        private Provider _provider;
        private List<Api.Repository.Appointment> _appointmentsRepository;
        private PatientInfo _patientInfo;

        [SetUp]
        public void Setup()
        {
            GalaSoft.MvvmLight.Threading.DispatcherHelper.Initialize();
            _dialogServiceMock = new Mock<IDialogService>();
            _loadingIndicatorShown = false;
            _dialogServiceMock.Setup(m => m.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action>()));
            _dialogServiceMock.Setup(m => m.ShowLoadingIndicator()).Callback(() => _loadingIndicatorShown = true);
            _dialogServiceMock.Setup(m => m.HideLoadingIndicator()).Callback(() => _loadingIndicatorShown = false);

            _provider = TestDataGenerator.GetProvider();
            _appointmentsPms = TestDataGenerator.GetAppointments("test provider id");
            _appointmentsRepository = _appointmentsPms.Select(TestDataGenerator.ToRepositoryAppointment).ToList();
            
            _apiMock = new Mock<IChewsiApi>();
            _apiMock.Setup(m => m.RegisterPlugin(It.IsAny<RegisterPluginRequest>()));
            
            _repositoryMock = new Mock<IRepository>();
            _repositoryMock.Setup(m => m.GetAppointments()).Returns(_appointmentsRepository);
            _repositoryMock.Setup(m => m.GetSettingValue<string>(Settings.PMS.TypeKey)).Returns(Settings.PMS.Types.Dentrix.ToString);
            _repositoryMock.Setup(m => m.Initialized).Returns(true);
            
            _settingsViewModelMock = new Mock<ISettingsViewModel>();

            _dentalApiFactoryService = new Mock<IDentalApiFactoryService>();
            _dentalApiMock = new Mock<IDentalApi>();
            _dentalApiMock.Setup(m => m.GetAppointmentsForToday()).Returns(_appointmentsPms);
            _dentalApiMock.Setup(m => m.GetProvider(It.IsAny<string>())).Returns(_provider);
            _patientInfo = new PatientInfo
            {
                ChewsiId = _appointmentsPms.First().ChewsiId,
                SubscriberFirstName = "test",
                SubscriberLastName = "sln",
                PatientFirstName = "pfn",
                PatientLastName = "pln",
                BirthDate = _appointmentsPms[0].Date
            };
            _dentalApiMock.Setup(m => m.GetPatientInfo(It.IsAny<string>())).Returns(_patientInfo);
            _dentalApiFactoryService.Setup(m => m.GetDentalApi(It.IsAny<Settings.PMS.Types>()))
                .Returns(_dentalApiMock.Object);
        }
        
        [Test]
        public void WhenRepositoryIsNotInitialized_DontCallChewsiApiItAndNoMessage()
        {
            // Arrange
            _repositoryMock.Setup(m => m.Initialized).Returns(false);    
                    
            // Act
            var appService = new AppService(_apiMock.Object, _repositoryMock.Object, _dialogServiceMock.Object, _settingsViewModelMock.Object);

            // Assert
            //AssertLoadingIndicator();
            Assert.IsFalse(_loadingIndicatorShown, "Loading is not hidden");

            _dialogServiceMock.Verify(m => m.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action>()), Times.Never, "Shouldn't have popup messages");
            _apiMock.Verify(m => m.RegisterPlugin(It.IsAny<RegisterPluginRequest>()), Times.Never, "Shouldn't register plugin");
            Assert.IsFalse(appService.Initialized);
        }

        [Test]
        public void WhenFirstRun_LoadAppointmentsAndShowSettingsWithDataFromFirstAppointment()
        {
            // Arrange
            var appService = new AppService(_apiMock.Object, _repositoryMock.Object, _dialogServiceMock.Object, 
                _settingsViewModelMock.Object);

            // Act
            appService.Initialize(true);
            DispatcherHelper.WaitWithDispatcher(1000);

            // Assert
            AssertLoadingIndicator();

            _dialogServiceMock.Verify(m => m.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action>()), Times.Never, "Shouldn't have popup messages");

            _apiMock.Verify(m => m.RegisterPlugin(It.IsAny<RegisterPluginRequest>()), Times.Never, "Shouldn't register plugin");

            Assert.IsTrue(appService.Initialized);
            _dentalApiMock.Verify(m => m.GetAppointmentsForToday(), Times.Once);

            _apiMock.Verify(m => m.Initialize(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()), Times.Never, "Should not initialize Chewsi API");

            AssertClaims(appService.ClaimItems, _appointmentsPms);

            _settingsViewModelMock.Verify(m => m.Fill(_provider.AddressLine1, _provider.AddressLine2, _provider.State, _provider.Tin, 
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once, "Should fill settings with values from appointment");
            _settingsViewModelMock.Verify(m => m.Show(It.IsAny<Action>()), Times.Once, "Should show settings popup");
        }

        [Test]
        public void WhenNotFirstRun_ShouldDisplayAppointmentsOnlyFromPms()
        {
            // Arrange
            var appService = new AppService(_apiMock.Object, _repositoryMock.Object, _dialogServiceMock.Object, 
                _settingsViewModelMock.Object);

            // Act
            appService.Initialize(false);
            DispatcherHelper.WaitWithDispatcher(1000);

            // Assert
            AssertLoadingIndicator();

            _dialogServiceMock.Verify(m => m.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action>()), Times.Never, "Shouldn't have popup messages");
            
            _apiMock.Verify(m => m.RegisterPlugin(It.IsAny<RegisterPluginRequest>()), Times.Never, "Shouldn't register plugin");

            Assert.IsTrue(appService.Initialized);

            _dentalApiMock.Verify(m => m.GetAppointmentsForToday(), Times.Once);
            
            _apiMock.Verify(m => m.Initialize(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), 
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "Should initialize Chewsi API");
            _apiMock.Verify(m => m.GetClaimProcessingStatus(It.IsAny<ClaimProcessingStatusRequest>()), Times.Never,
                "Should not load claim statuses from Chewsi API");

            AssertClaims(appService.ClaimItems, _appointmentsPms);

            _settingsViewModelMock.Verify(m => m.Fill(_provider.AddressLine1, _provider.AddressLine2, _provider.State, _provider.Tin,
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never, "Should not touch settings model");
            _settingsViewModelMock.Verify(m => m.Show(It.IsAny<Action>()), Times.Never, "Should not show settings popup");
        }

        [Test]
        public void WhenNotFirstRunAndSomeAppointmentsWereSubmittedBefore_ShouldDisplayAppointmentsFromPmsAndService()
        {
            // Arrange
            var statusResponse = new ClaimProcessingStatusResponse();
            var submittedClaims = TestDataGenerator.GetAppointments(_appointmentsPms[0].ProviderId)
                .Take(5)
                .Select(TestDataGenerator.ToRepositoryAppointment)
                .ToList();
            statusResponse.AddRange(submittedClaims.Select(TestDataGenerator.ToClaimStatus));

            foreach (var appointment in _appointmentsRepository.Take(5))
            {
                appointment.State = AppointmentState.ValidationCompletedAndClaimSubmitted;
            }
            _repositoryMock.Setup(m => m.GetAppointments()).Returns(_appointmentsRepository);

            _apiMock.Setup(m => m.GetClaimProcessingStatus(It.IsAny<ClaimProcessingStatusRequest>())).Returns(statusResponse);

            var appService = new AppService(_apiMock.Object, _repositoryMock.Object, _dialogServiceMock.Object, 
                _settingsViewModelMock.Object);

            // Act
            appService.Initialize(false);
            DispatcherHelper.WaitWithDispatcher(20000);

            // Assert
            AssertLoadingIndicator();

            _dialogServiceMock.Verify(m => m.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action>()), Times.Never, "Shouldn't have popup messages");
            
            _apiMock.Verify(m => m.RegisterPlugin(It.IsAny<RegisterPluginRequest>()), Times.Never, "Shouldn't register plugin");

            Assert.IsTrue(appService.Initialized);

            _dentalApiMock.Verify(m => m.GetAppointmentsForToday(), Times.Once);
            
            _apiMock.Verify(m => m.Initialize(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), 
                It.IsAny<string>(), It.IsAny<string>()), Times.Once, "Should initialize Chewsi API");
            _apiMock.Verify(m => m.GetClaimProcessingStatus(It.IsAny<ClaimProcessingStatusRequest>()), Times.Once, 
                "Should load claim statuses from Chewsi API");

            // skip last 5 (claim statuses)
            AssertClaims(appService.ClaimItems.Take(_appointmentsPms.Count - 5).ToList(), _appointmentsPms);
            
            foreach (var claimItem in appService.ClaimItems.Skip(_appointmentsPms.Count))
            {
                Assert.IsTrue(
                    submittedClaims.Any(
                        m =>
                            m.PatientId == claimItem.PatientId &&
                            m.DateTime == claimItem.Date &&
                            m.ChewsiId == claimItem.ChewsiId), "AppService doesn't display all claim statuses");
            }

            _settingsViewModelMock.Verify(m => m.Fill(_provider.AddressLine1, _provider.AddressLine2, _provider.State, _provider.Tin,
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never, "Should not touch settings model");
            _settingsViewModelMock.Verify(m => m.Show(It.IsAny<Action>()), Times.Never, "Should not show settings popup");
        }
        
        [Test]
        public void WhenPmsVersionChanged_ShouldUpdatePluginRegistration()
        {
            // Arrange
            _repositoryMock.Setup(m => m.GetSettingValue<string>(Settings.PMS.VersionKey)).Returns("v1");
            _repositoryMock.Setup(m => m.GetSettingValue<string>(Settings.OsKey)).Returns(Utils.GetOperatingSystemInfo());
            _repositoryMock.Setup(m => m.GetSettingValue<string>(Settings.AppVersionKey)).Returns(Utils.GetPluginVersion());
            _dentalApiMock.Setup(m => m.GetVersion()).Returns("v2");
            var appService = new AppService(_apiMock.Object, _repositoryMock.Object, _dialogServiceMock.Object,
                _settingsViewModelMock.Object);

            // Act
            appService.Initialize(false);
            DispatcherHelper.WaitWithDispatcher(1000);

            // Assert
            AssertLoadingIndicator();
            _apiMock.Verify(m => m.UpdatePluginRegistration(It.IsAny<UpdatePluginRegistrationRequest>()), Times.Once);
        }

        [Test]
        public void WhenPmsVersionNotChanged_ShouldNotUpdatePluginRegistration()
        {
            // Arrange
            _repositoryMock.Setup(m => m.GetSettingValue<string>(Settings.PMS.VersionKey)).Returns("v1");
            _repositoryMock.Setup(m => m.GetSettingValue<string>(Settings.OsKey)).Returns(Utils.GetOperatingSystemInfo());
            _repositoryMock.Setup(m => m.GetSettingValue<string>(Settings.AppVersionKey)).Returns(Utils.GetPluginVersion());
            _dentalApiMock.Setup(m => m.GetVersion()).Returns("v1");
            var appService = new AppService(_apiMock.Object, _repositoryMock.Object, _dialogServiceMock.Object,
                _settingsViewModelMock.Object);

            // Act
            appService.Initialize(false);
            DispatcherHelper.WaitWithDispatcher(1000);

            // Assert
            AssertLoadingIndicator();
            _apiMock.Verify(m => m.UpdatePluginRegistration(It.IsAny<UpdatePluginRegistrationRequest>()), Times.Never);
        }
        
        [Test]
        public void WhenSubmittingClaim_ShouldUpdateList()
        {
            // Arrange
            var appService = new AppService(_apiMock.Object, _repositoryMock.Object, _dialogServiceMock.Object,
                _settingsViewModelMock.Object);
            appService.Initialize(false);
            var procedure = new ProcedureInfo
            {
                Date = DateTime.Now,
                Amount = 123,
                Code = "1234"
            };
            _dentalApiMock.Setup(m => m.GetProcedures(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .Returns(new List<ProcedureInfo> { procedure });
            _repositoryMock.Setup(m => m.GetAppointmentById(It.IsAny<string>())).Returns(_appointmentsRepository[0]);
            _repositoryMock.Setup(m => m.UpdateAppointment(It.IsAny<Api.Repository.Appointment>()))
                .Callback(() => _appointmentsRepository[0].State = AppointmentState.ValidationCompletedAndClaimSubmitted);
            _apiMock.Setup(m => m.ValidateSubscriberAndProvider(
                It.IsAny<ProviderInformation>(),
                It.IsAny<ProviderAddressInformation>(),
                It.IsAny<SubscriberInformation>())).Returns(new ValidateSubscriberAndProviderResponse
                {
                    ProviderValidationStatus = "Valid",
                    SubscriberValidationStatus = "Valid"
                });
            DispatcherHelper.WaitWithDispatcher(2000);
            var claim = appService.ClaimItems.First();
            bool callback = false;

            // Act
            appService.ValidateAndSubmitClaim(claim.Id, claim.Date, claim.ProviderId, claim.PatientId, () =>
            {
                callback = true;
            });
            DispatcherHelper.WaitWithDispatcher(2000);

            // Assert
            AssertLoadingIndicator();
            Assert.IsTrue(callback);
            // skip first 1 (submitted)
            AssertClaims(appService.ClaimItems, _appointmentsPms.Skip(1).ToList());
            _apiMock.Verify(m => m.ValidateSubscriberAndProvider(
                It.Is<ProviderInformation>(n => n.NPI == _provider.Npi
                                                && n.TIN == _provider.Tin),
                It.Is<ProviderAddressInformation>(n => n.RenderingAddress1 == _provider.AddressLine1
                                                       && n.RenderingCity == _provider.City
                                                       && n.RenderingAddress2 == _provider.AddressLine2
                                                       && n.RenderingState == _provider.State
                                                       && n.RenderingZip == _provider.ZipCode),
                It.Is<SubscriberInformation>(n => n.Id == _patientInfo.ChewsiId
                                                  && n.SubscriberFirstName == _patientInfo.SubscriberFirstName
                                                  && n.SubscriberLastName == _patientInfo.SubscriberLastName
                                                  && n.SubscriberDateOfBirth == _patientInfo.BirthDate)), Times.Once);
            _apiMock.Verify(m => m.ProcessClaim(
                It.Is<ProviderInformation>(n => n.NPI == _provider.Npi
                                                && n.TIN == _provider.Tin),
                It.Is<SubscriberInformation>(n => n.Id == _patientInfo.ChewsiId
                                                  && n.PatientFirstName == _patientInfo.PatientFirstName
                                                  && n.PatientLastName == _patientInfo.PatientLastName
                                                  && n.SubscriberFirstName == _patientInfo.SubscriberFirstName
                                                  && n.SubscriberLastName == _patientInfo.SubscriberLastName
                                                  && n.SubscriberDateOfBirth == _patientInfo.BirthDate),
                It.Is<List<ClaimLine>>(n =>
                    n[0].DateOfService == procedure.Date.ToString("d")
                    && n[0].ProcedureCharge == procedure.Amount.ToString("F")
                    && n[0].ProcedureCode == procedure.Code)
                ), Times.Once);
            AssertLoadingIndicator();
            _dialogServiceMock.Verify(m => m.Show(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action>()), Times.Never, "Shouldn't have popup messages");
        }

        private void AssertLoadingIndicator()
        {
            _dialogServiceMock.Verify(m => m.ShowLoadingIndicator(), Times.AtLeastOnce);
            _dialogServiceMock.Verify(m => m.HideLoadingIndicator(), Times.AtLeastOnce);
            Assert.IsFalse(_loadingIndicatorShown, "Loading is not hidden");
        }

        private static void AssertClaims(IEnumerable<ClaimItemViewModel> models, List<Appointment> appointments)
        {
            foreach (var claimItem in models)
            {
                Assert.IsTrue(
                    appointments.Any(
                        m =>
                            m.PatientId == claimItem.PatientId &&
                            m.Date == claimItem.Date &&
                            m.ChewsiId == claimItem.ChewsiId), "AppService doesn't display all claims");
            }
        }
    }
}
