using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;

namespace Nop.Plugin.Payments.Eigen.Models
{
    public class PaymentInfoModel
    {
        public PaymentInfoModel()
        {
            CreditCardTypes = new List<SelectListItem>();
            ExpireMonths = new List<SelectListItem>();
            ExpireYears = new List<SelectListItem>();
            Countries = new List<SelectListItem>();
        }

        [NopResourceDisplayName("Payment.BillingAddress")]
        [AllowHtml]
        public Address BillingAddress { get; set; }


        [NopResourceDisplayName("Payment.SelectCreditCard")]
        [AllowHtml]
        public string CreditCardType { get; set; }
        [NopResourceDisplayName("Payment.SelectCreditCard")]
        public IList<SelectListItem> CreditCardTypes { get; set; }

        [NopResourceDisplayName("Payment.CardholderName")]
        [AllowHtml]
        public string CardholderName { get; set; }

        [NopResourceDisplayName("Payment.CardNumber")]
        [AllowHtml]
        public string CardNumber { get; set; }

        [NopResourceDisplayName("Payment.ExpirationDate")]
        [AllowHtml]
        public string ExpireMonth { get; set; }
        [NopResourceDisplayName("Payment.ExpirationDate")]
        [AllowHtml]
        public string ExpireYear { get; set; }
        public IList<SelectListItem> ExpireMonths { get; set; }
        public IList<SelectListItem> ExpireYears { get; set; }

        [NopResourceDisplayName("Payment.CardCode")]
        [AllowHtml]
        public string CardCode { get; set; }

        [NopResourceDisplayName("Payment.Email")]
        [AllowHtml]
        public string Email { get; set; }

        [NopResourceDisplayName("Payment.PhoneNumber")]
        [AllowHtml]
        public string PhoneNumber { get; set; }

        [NopResourceDisplayName("Payment.Address1")]
        [AllowHtml]
        public string Address1 { get; set; }

        [NopResourceDisplayName("Payment.City")]
        [AllowHtml]
        public string City { get; set; }

        [NopResourceDisplayName("Payment.StateProvinceId")]
        [AllowHtml]
        public string StateProvinceId { get; set; }

        [NopResourceDisplayName("Payment. ZipPostalCode")]
        [AllowHtml]
        public string ZipPostalCode { get; set; }

        [NopResourceDisplayName("Payment.CountryId")]
        [AllowHtml]
        public string Country { get; set; }

        [NopResourceDisplayName("Payment.Booking")]
        [AllowHtml]
        public string Booking { get; set; }

        [NopResourceDisplayName("Payment.AeroplanNumber")]
        [AllowHtml]
        public string AeroplanNumber { get; set; }

        [NopResourceDisplayName("Payment.NamePassport")]
        [AllowHtml]
        public string NamePassport { get; set; }

        [NopResourceDisplayName("Payment.PassportNumber")]
        [AllowHtml]
        public string PassportNumber { get; set; }

        [NopResourceDisplayName("Payment.Countries")]
        [AllowHtml]
        //public IList<Country> Countries { get; set; }
        public IList<SelectListItem> Countries { get; set; }

        [NopResourceDisplayName("Payment.subscribeYN")]        
        public bool subscribeYN { get; set; }

        [NopResourceDisplayName("Payment.Voucher")]
        [AllowHtml]
        public string Voucher { get; set; }

        public bool IsDisplayVoucher { get; set; }

    }
}
