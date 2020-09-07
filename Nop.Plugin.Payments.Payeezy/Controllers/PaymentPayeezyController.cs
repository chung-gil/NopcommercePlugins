using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Payeezy.Models;
using Nop.Plugin.Payments.Payeezy.Validators;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Nop.Services.Flights;
using Nop.Core.Domain.Flight;
using Nop.Services.Customers;
using Nop.Services.Directory;

namespace Nop.Plugin.Payments.Payeezy.Controllers
{
    public class PaymentPayeezyController : BasePaymentController
    {
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;
        private readonly IPaymentService _paymentService;
        private readonly PaymentSettings _paymentSettings;
        private readonly IFlightService _flightsService;
        private readonly ICustomerService _customerService;
        private readonly ICountryService _countryService;

        public PaymentPayeezyController(ILocalizationService localizationService,
            ISettingService settingService,
            IStoreService storeService,
            IWorkContext workContext,
            IPaymentService paymentService,
            IFlightService flightsService,
            ICustomerService customerService,
            ICountryService countryService,
            PaymentSettings paymentSettings)
        {
            this._localizationService = localizationService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._workContext = workContext;
            this._paymentService = paymentService;
            this._paymentSettings = paymentSettings;
            this._flightsService = flightsService;
            this._customerService = customerService;
            this._countryService = countryService;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payeezyPaymentSettings = _settingService.LoadSetting<PayeezyPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = payeezyPaymentSettings.UseSandbox,
                ExactID = payeezyPaymentSettings.ExactID,
                Password = payeezyPaymentSettings.Password,
                keyID = payeezyPaymentSettings.keyID,
                Hmackey = payeezyPaymentSettings.Hmackey,
                AdditionalFee = payeezyPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = payeezyPaymentSettings.AdditionalFeePercentage,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(payeezyPaymentSettings, x => x.UseSandbox, storeScope);
                model.ExactID_OverrideForStore = _settingService.SettingExists(payeezyPaymentSettings, x => x.ExactID, storeScope);
                model.Password_OverrideForStore = _settingService.SettingExists(payeezyPaymentSettings, x => x.Password, storeScope);
                model.keyID_OverrideForStore = _settingService.SettingExists(payeezyPaymentSettings, x => x.keyID, storeScope);
                model.Hmackey_OverrideForStore = _settingService.SettingExists(payeezyPaymentSettings, x => x.Hmackey, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(payeezyPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(payeezyPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            }

            return View("~/Plugins/Payments.Payeezy/Views/PaymentPayeezy/Configure.cshtml", model);
        }


        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payeezyPaymentSettings = _settingService.LoadSetting<PayeezyPaymentSettings>(storeScope);

            //save settings
            payeezyPaymentSettings.UseSandbox = model.UseSandbox;
            payeezyPaymentSettings.ExactID = model.ExactID;
            payeezyPaymentSettings.Password = model.Password;
            payeezyPaymentSettings.keyID = model.keyID;
            payeezyPaymentSettings.Hmackey = model.Hmackey;
            payeezyPaymentSettings.AdditionalFee = model.AdditionalFee;
            payeezyPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(payeezyPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payeezyPaymentSettings, x => x.ExactID, model.ExactID_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payeezyPaymentSettings, x => x.Password, model.Password_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payeezyPaymentSettings, x => x.keyID, model.keyID_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payeezyPaymentSettings, x => x.Hmackey, model.Hmackey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payeezyPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(payeezyPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();

            var customer = _customerService.GetCustomerById(_workContext.CurrentCustomer.Id);

            model.Customers = customer;

            IList<SelectListItem> listAddress = new List<SelectListItem>();
            listAddress.Add(new SelectListItem() { Text = "Select Address", Value = "0" });
            model.ListAddress = listAddress;

            foreach (var c in customer.Addresses)
            {
                model.ListAddress.Add(new SelectListItem() { Text = c.Email + " - " + c.Address1, Value = c.Id.ToString() });
            }

            //years
            for (var i = 0; i < 15; i++)
            {
                var year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem
                {
                    Text = year,
                    Value = year,
                });
            }

            //months
            for (var i = 1; i <= 12; i++)
            {
                var text = (i < 10) ? "0" + i : i.ToString();
                model.ExpireMonths.Add(new SelectListItem
                {
                    Text = text,
                    Value = i.ToString(),
                });
            }


            IList<SelectListItem> listCountries = new List<SelectListItem>();
            listCountries.Add(new SelectListItem() { Text = "Select country", Value = "" });
            model.Countries = listCountries;

            foreach (var c in _countryService.GetAllCountries())
            {
                model.Countries.Add(new SelectListItem() { Text = c.Name, Value = c.Name });
            }


            //set postback values
            var form = this.Request.Form;
            model.CardholderName = form["CardholderName"];
            model.CardNumber = form["CardNumber"];
            model.CardCode = form["CardCode"];
            var selectedMonth = model.ExpireMonths.FirstOrDefault(x => x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));

            if (selectedMonth != null)
                selectedMonth.Selected = true;

            var selectedYear = model.ExpireYears.FirstOrDefault(x => x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));

            if (selectedYear != null)
                selectedYear.Selected = true;

            return View("~/Plugins/Payments.Payeezy/Views/PaymentPayeezy/PaymentInfo.cshtml", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);

            string aeroplanNumber = form["AeroplanNumber"];
            if (!string.IsNullOrEmpty(aeroplanNumber))
            {
                aeroplanNumber = string.Join("", aeroplanNumber.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
            }

            string voucherNum = form["Voucher"];
            if (!string.IsNullOrEmpty(voucherNum))
            {
                voucherNum = string.Join("", voucherNum.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
            }


            var model = new PaymentInfoModel
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"],
                Email = form["Email"],
                PhoneNumber = form["PhoneNumber"],
                City = form["City"],
                ZipPostalCode = form["ZipPostalCode"],
                Booking = form["Booking"],
                NamePassport = form["NamePassport"],
                PassportNumber = form["PassportNumber"],
                Country = form["Countries"],
                Address1 = form["Address1"],
                Voucher = voucherNum
            };

            var validationResult = validator.Validate(model);

            if (!validationResult.IsValid)
                warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            /*
            var paymentInfo = new ProcessPaymentRequest
            {
                //CreditCardType is not used by Authorize.NET
                CreditCardName = form["CardholderName"],
                CreditCardNumber = form["CardNumber"],
                CreditCardExpireMonth = int.Parse(form["ExpireMonth"]),
                CreditCardExpireYear = int.Parse(form["ExpireYear"]),
                CreditCardCvv2 = form["CardCode"]
            };
            */

            var paymentInfo = new ProcessPaymentRequest();

            _workContext.CurrentCustomer.BillingAddress.FirstName = form["CardholderName"];
            _workContext.CurrentCustomer.BillingAddress.Email = form["Email"];
            _workContext.CurrentCustomer.BillingAddress.PhoneNumber = form["PhoneNumber"];
            _workContext.CurrentCustomer.BillingAddress.Address1 = form["Address1"];
            _workContext.CurrentCustomer.BillingAddress.State = form["StateProvinceId"];

            if (form["Countries"] != "Select country")
            {
                _workContext.CurrentCustomer.BillingAddress.CountryN = form["Countries"];
            }

            _workContext.CurrentCustomer.BillingAddress.City = form["City"];
            _workContext.CurrentCustomer.BillingAddress.ZipPostalCode = form["ZipPostalCode"];
            _workContext.CurrentCustomer.BillingAddress.Booking = form["Booking"];
            //_workContext.CurrentCustomer.BillingAddress.AeroplanNumber = form["AeroplanNumber"];            
            string aeroplanNumber = form["AeroplanNumber"];
            if (!string.IsNullOrEmpty(aeroplanNumber))
            {
                aeroplanNumber = string.Join("", aeroplanNumber.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
            }

            string voucherNum = form["Voucher"];
            if (!string.IsNullOrEmpty(voucherNum))
            {
                voucherNum = string.Join("", voucherNum.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
            }

            _workContext.CurrentCustomer.BillingAddress.AeroplanNumber = aeroplanNumber;
            _workContext.CurrentCustomer.BillingAddress.NamePassport = form["NamePassport"];
            _workContext.CurrentCustomer.BillingAddress.PassportNumber = form["PassportNumber"];
            //_workContext.CurrentCustomer.BillingAddress.subscribeYN = form["subscribeYN"].Contains("true") ? true : false;
            _workContext.CurrentCustomer.BillingAddress.Voucher = voucherNum;

            _customerService.UpdateCustomer(_workContext.CurrentCustomer);
            paymentInfo.CreditCardType = form["CreditCardType"];
            paymentInfo.CreditCardName = form["CardholderName"];
            paymentInfo.CreditCardNumber = form["CardNumber"];
            paymentInfo.CreditCardExpireMonth = int.Parse(form["ExpireMonth"]);
            paymentInfo.CreditCardExpireYear = int.Parse(form["ExpireYear"]);
            paymentInfo.CreditCardCvv2 = form["CardCode"];

            Flights flightInfo = _flightsService.GetFlightById(Convert.ToInt32(_workContext.CurrentCustomer.BillingAddress.Flight));

            paymentInfo.FlightInfo = "[" + flightInfo.FlightNumber + "]" + _workContext.CurrentCustomer.BillingAddress.Departure.Replace("-", "/");

            return paymentInfo;
        }       
    }
}
