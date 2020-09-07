using FluentValidation;
using Nop.Plugin.Payments.Payeezy.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.Payeezy.Validators
{
    public class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
    {
        public PaymentInfoValidator(ILocalizationService localizationService)
        {
            //useful links:
            //http://fluentvalidation.codeplex.com/wikipage?title=Custom&referringTitle=Documentation&ANCHOR#CustomValidator
            //http://benjii.me/2010/11/credit-card-validator-attribute-for-asp-net-mvc-3/

            //RuleFor(x => x.CardNumber).NotEmpty().WithMessage(localizationService.GetResource("Payment.CardNumber.Required"));
            //RuleFor(x => x.CardCode).NotEmpty().WithMessage(localizationService.GetResource("Payment.CardCode.Required"));

            RuleFor(x => x.CardholderName).NotEmpty().WithMessage(localizationService.GetResource("Payment.CardholderName.Required"));
            RuleFor(x => x.CardNumber).IsCreditCard().WithMessage(localizationService.GetResource("Payment.CardNumber.Wrong"));
            RuleFor(x => x.CardCode).Matches(@"^[0-9]{3,4}$").WithMessage(localizationService.GetResource("Payment.CardCode.Wrong"));
            RuleFor(x => x.ExpireMonth).NotEmpty().WithMessage(localizationService.GetResource("Payment.ExpireMonth.Required"));
            RuleFor(x => x.ExpireYear).NotEmpty().WithMessage(localizationService.GetResource("Payment.ExpireYear.Required"));
            RuleFor(x => x.Email).NotEmpty().WithMessage(localizationService.GetResource("Payment.Email.Required"));
            RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage(localizationService.GetResource("Payment.PhoneNumber.Required"));
            RuleFor(x => x.City).NotEmpty().WithMessage(localizationService.GetResource("Payment.City.Required"));
            RuleFor(x => x.ZipPostalCode).NotEmpty().WithMessage(localizationService.GetResource("Payment.ZipPostalCode.Required"));
            RuleFor(x => x.Booking).NotEmpty().WithMessage(localizationService.GetResource("Payment.Booking.Required"));
            RuleFor(x => x.NamePassport).NotEmpty().WithMessage(localizationService.GetResource("Payment.NamePassport.Required"));
            RuleFor(x => x.PassportNumber).NotEmpty().WithMessage(localizationService.GetResource("Payment.PassportNumber.Required"));
            RuleFor(x => x.Address1).NotEmpty().WithMessage(localizationService.GetResource("Payment.Street.Required"));
            RuleFor(x => x.Country).NotEmpty().WithMessage(localizationService.GetResource("Payment.Country.Required"));
        }
    }
}