using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Interfaces;
using ChewsiPlugin.Api.Repository;
using ChewsiPlugin.UI.ViewModels;
using ChewsiPlugin.UI.ViewModels.DialogService;
using Moq;
using NUnit.Framework;

namespace ChewsiPlugin.Tests
{
    internal class Appointment : IAppointment
    {
        public DateTime Date { get; set; }
        public string InsuranceId { get; set; }
        public bool IsCompleted { get; }
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string ProviderId { get; set; }
    }
    
    [TestFixture]
    public class MainViewModelTests
    {
        [Test]
        public void WhenDentalApiIsNotSet_DontCallItAndShowMessage()
        {
            // Arrange
            var repositoryMock = new Mock<Repository>();
            var dialogServiceMock = new Mock<IDialogService>();
            dialogServiceMock.Setup(m => m.Show(It.IsAny<string>(), It.IsAny<string>()));
            var apiMock = new Mock<IChewsiApi>();
            apiMock.Setup(m => m.Initialize(It.IsAny<InitializeRequest>()));
            
            // Act
            var model = new MainViewModel(null, repositoryMock.Object, dialogServiceMock.Object, apiMock.Object);

            // Assert
            dialogServiceMock.Verify(m => m.Show(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            apiMock.Verify(m => m.Initialize(It.IsAny<InitializeRequest>()), Times.Never);
        }

        [Test]
        public void WhenDentalApiIsSet_ShouldLoadAppointments()
        {
            // Arrange
            var repositoryMock = new Mock<Repository>();

            var dentalApiMock = new Mock<IDentalApi>();
            var appointments = GetAppointments();
            dentalApiMock.Setup(m => m.GetAppointmentsForToday()).Returns(appointments);
            
            var apiMock = new Mock<IChewsiApi>();
            apiMock.Setup(m => m.Initialize(It.IsAny<InitializeRequest>()));

            var dialogServiceMock = new Mock<IDialogService>();

            // Act
            var model = new MainViewModel(dentalApiMock.Object, repositoryMock.Object, dialogServiceMock.Object, apiMock.Object);
            Thread.Sleep(3000); // wait till background thread loads appointments

            // Assert
            dialogServiceMock.Verify(m => m.Show(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            dentalApiMock.Verify(m => m.GetAppointmentsForToday(), Times.Once);
            apiMock.Verify(m => m.Initialize(It.IsAny<InitializeRequest>()), Times.Never);
            foreach (var claimItem in model.ClaimItems)
            {
                Assert.IsTrue(
                    appointments.Any(
                        m =>
                            m.PatientId == claimItem.PatientId && 
                            m.Date == claimItem.Date &&
                            m.InsuranceId == claimItem.InsuranceId));
            }
        }

        internal List<IAppointment> GetAppointments()
        {
            return new List<IAppointment>(Enumerable.Range(0, 30).Select(m => GetAppointment()).ToList());
        }

        readonly Random _random = new Random();

        private Appointment GetAppointment()
        {
            return new Appointment
            {
                InsuranceId = _random.Next(10000, 100000).ToString(),
                PatientName = "John Smith #" + _random.Next(100, 1000),
                ProviderId = _random.Next(100, 1000).ToString(),
                PatientId = _random.Next(100, 1000).ToString(),
                Date = DateTime.Now
            };
        }
    }
}
