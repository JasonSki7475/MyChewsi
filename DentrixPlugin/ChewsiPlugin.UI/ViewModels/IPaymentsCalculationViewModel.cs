using ChewsiPlugin.Api.Common;

namespace ChewsiPlugin.UI.ViewModels
{
    internal interface IPaymentsCalculationViewModel
    {
        void Hide();
        void Show(CalculatedPaymentsDto m);
        bool IsVisible { get; }
    }
}