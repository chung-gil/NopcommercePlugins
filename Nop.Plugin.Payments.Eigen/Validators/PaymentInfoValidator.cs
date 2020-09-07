using FluentValidation;
using Nop.Plugin.Payments.Eigen.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using System;

namespace Nop.Plugin.Payments.Eigen.Validators
{
    public class PaymentInfoValidator : AbstractValidator<PaymentInfoModel>
    {
        public PaymentInfoValidator(ILocalizationService localizationService)
        {
            //useful links:
            //http://fluentvalidation.codeplex.com/wikipage?title=Custom&referringTitle=Documentation&ANCHOR#CustomValidator
            //http://benjii.me/2010/11/credit-card-validator-attribute-for-asp-net-mvc-3/
                        
            RuleFor(x => x.CardholderName).NotEmpty().WithMessage(localizationService.GetResource("Payment.CardholderName.Required"));
            RuleFor(x => x.CardNumber).IsCreditCard().WithMessage(localizationService.GetResource("Payment.CardNumber.Wrong"));
            RuleFor(x => x.CardCode).Matches(@"^[0-9]{3,4}$").WithMessage(localizationService.GetResource("Payment.CardCode.Wrong"));
            RuleFor(x => x.ExpireYear).Must(x => Convert.ToInt32(x) >= DateTime.Now.Year).When(f => !String.IsNullOrEmpty(f.ExpireYear)).WithMessage(localizationService.GetResource("Payment.ExpireYear.Required"));
            RuleFor(x => x.ExpireMonth).Must(x => Convert.ToInt32(x) >= DateTime.Now.Month).When(f => !String.IsNullOrEmpty(f.ExpireMonth) && !String.IsNullOrEmpty(f.ExpireYear) && Convert.ToInt32(f.ExpireYear) == DateTime.Now.Year).WithMessage(localizationService.GetResource("Payment.ExpireMonth.Required"));
            RuleFor(x => x.Email).NotEmpty().WithMessage(localizationService.GetResource("Payment.Email.Required"));
            RuleFor(x => x.Email).EmailAddress().WithMessage(localizationService.GetResource("Common.WrongEmail"));
            RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage(localizationService.GetResource("Payment.PhoneNumber.Required"));
            RuleFor(x => x.City).NotEmpty().WithMessage(localizationService.GetResource("Payment.City.Required"));            
            RuleFor(x => x.ZipPostalCode).NotEmpty().WithMessage(localizationService.GetResource("Payment.ZipPostalCode.Required"));            
            RuleFor(x => x.Booking).NotEmpty().WithMessage(localizationService.GetResource("Payment.Booking.Required"));
            RuleFor(x => x.NamePassport).NotEmpty().WithMessage(localizationService.GetResource("Payment.NamePassport.Required"));
            RuleFor(x => x.PassportNumber).NotEmpty().WithMessage(localizationService.GetResource("Payment.PassportNumber.Required"));
            //RuleFor(x => x.AeroplanNumber).Matches(@"^\d{9}$").WithMessage(localizationService.GetResource("Payment.Aero.Required"));
            When(x => !String.IsNullOrEmpty(x.AeroplanNumber),
               () =>
               {
                   RuleFor(x => x.AeroplanNumber).Matches(@"^\d{9}$")
                   .WithMessage(localizationService.GetResource("Payment.AeroplanNumber.Wrong"));
               });
            
        }
    }
}
