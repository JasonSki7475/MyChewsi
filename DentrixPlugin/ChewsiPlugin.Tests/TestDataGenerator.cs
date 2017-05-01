using System;
using System.Collections.Generic;
using System.Linq;
using ChewsiPlugin.Api.Chewsi;
using ChewsiPlugin.Api.Common;
using ChewsiPlugin.Api.Repository;
using Appointment = ChewsiPlugin.Api.Common.Appointment;

namespace ChewsiPlugin.Tests
{
    internal static class TestDataGenerator
    {
        public static List<Appointment> GetAppointments(string providerId)
        {
            return new List<Appointment>(Enumerable.Range(0, 30).Select(m => GetAppointment(providerId)).ToList());
        }

        static readonly Random Random = new Random();

        public static Appointment GetAppointment(string providerId)
        {
            return new Appointment
            {
                ChewsiId = "Test Chewsi Id; should NOT be unique in tests (DentalApi.GetPatientInfo)",
                PatientName = "John Smith #" + Random.Next(100, 1000),
                ProviderId = providerId,
                PatientId = Random.Next(100, 1000).ToString(),
                Date = DateTime.Now
            };
        }

        public static Provider GetProvider()
        {
            return new Provider
            {
                State = Random.Next(100, 1000).ToString(),
                Tin = Random.Next(100, 1000).ToString(),
                City = Random.Next(100, 1000).ToString(),
                AddressLine1 = Random.Next(100, 1000).ToString(),
                Npi = Random.Next(100, 1000).ToString(),
                ZipCode = Random.Next(100, 1000).ToString(),
                AddressLine2 = Random.Next(100, 1000).ToString()
            };
        }

        public static Api.Repository.Appointment ToRepositoryAppointment(Appointment m)
        {
            return new Api.Repository.Appointment
            {
                ChewsiId = m.ChewsiId,
                SubscriberFirstName = m.PatientName,
                DateTime = m.Date,
                State = AppointmentState.TreatmentCompleted,
                Id = Random.Next(10000, 100000).ToString(),
                PatientId = m.PatientId,
                StatusText = Random.Next(10000, 100000).ToString(),
                ProviderId = m.ProviderId,
                PatientName = m.PatientName
            };
        }

        public static ClaimStatus ToClaimStatus(Api.Repository.Appointment claim)
        {
            return new ClaimStatus
            {
                SubscriberFirstName = claim.SubscriberFirstName,
                ChewsiID = claim.ChewsiId,
                ProviderId = claim.ProviderId,
                PostedOnDate = claim.DateTime,
                MessageToDisplay = "test",
                PatientFirstName = claim.PatientName,
                Status = claim.State.ToString()
            };
        }
    }
}
