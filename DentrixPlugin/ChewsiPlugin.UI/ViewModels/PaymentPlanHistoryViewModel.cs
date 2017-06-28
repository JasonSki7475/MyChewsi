using System;
using System.Collections.Generic;
using System.Linq;
using ChewsiPlugin.Api.Common;
using GalaSoft.MvvmLight;

namespace ChewsiPlugin.UI.ViewModels
{
    internal class PaymentPlanHistoryViewModel : ViewModelBase
    {
        public PaymentPlanHistoryViewModel(string chewsiId, List<PaymentPlanHistoryItemDto> items, string lastPaymentOn, string patientFirstName, string paymentSchedule, DateTime postedOn, string provider, string balanceRemaining, string nextPaymentOn)
        {
            ChewsiId = chewsiId;
            Items =
                items.Select(
                    m => new PaymentPlanHistoryItemViewModel(m.ChewsiFeeAmount, m.PatientPaymentOf, m.PaymentMadeOn,
                        m.PaymentSchedule, m.ProviderReceives)).ToList();
            LastPaymentOn = lastPaymentOn;
            PatientFirstName = patientFirstName;
            PaymentSchedule = paymentSchedule;
            PostedOn = postedOn;
            Provider = provider;
            BalanceRemaining = balanceRemaining;
            NextPaymentOn = nextPaymentOn;
        }

        public DateTime PostedOn { get; set; }
        public string ChewsiId { get; set; }
        public string PatientFirstName { get; set; }
        public string Provider { get; set; }
        public string PaymentSchedule { get; set; }
        public string LastPaymentOn { get; set; }
        public string BalanceRemaining { get; set; }
        public string NextPaymentOn { get; set; }
        public List<PaymentPlanHistoryItemViewModel> Items { get; set; }
    }

    internal class PaymentPlanHistoryItemViewModel : ViewModelBase
    {
        public PaymentPlanHistoryItemViewModel(string chewsiFeeAmount, string patientPaymentOf, string paymentMadeOn, string paymentSchedule, string providerReceives)
        {
            ChewsiFeeAmount = chewsiFeeAmount;
            PatientPaymentOf = patientPaymentOf;
            PaymentMadeOn = paymentMadeOn;
            PaymentSchedule = paymentSchedule;
            ProviderReceives = providerReceives;
        }

        public string PaymentSchedule { get; set; }
        public string PaymentMadeOn { get; set; }
        public string PatientPaymentOf { get; set; }
        public string ChewsiFeeAmount { get; set; }
        public string ProviderReceives { get; set; }
    }
}
