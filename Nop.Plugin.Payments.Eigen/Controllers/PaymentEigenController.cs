using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.Eigen.Models;
using Nop.Plugin.Payments.Eigen.Validators;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Services.Directory;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Services.Common;
using Nop.Core.Domain;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Flights;
using Nop.Core.Domain.Flight;

namespace Nop.Plugin.Payments.Eigen.Controllers
{
    public class PaymentEigenController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly IFlightService _flightsService;
        private readonly IDiscountService _discountService;

        public PaymentEigenController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService, 
            ILocalizationService localizationService,
            IAddressService addressService,
            ICountryService countryService,
            ICustomerService customerService,
            IFlightService flightsService,
            IDiscountService discountService)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._localizationService = localizationService;
            this._addressService = addressService;
            this._countryService = countryService;
            this._customerService = customerService;
            this._flightsService = flightsService;
            this._discountService = discountService;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var eigenPaymentSettings = _settingService.LoadSetting<EigenPaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.UseSandbox = eigenPaymentSettings.UseSandbox;
            model.AllowStoringTransactionLog = eigenPaymentSettings.AllowStoringTransactionLog;
            model.TerminalID = eigenPaymentSettings.TerminalID;
            model.HashPassword = eigenPaymentSettings.HashPassword;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(eigenPaymentSettings, x => x.UseSandbox, storeScope);
                model.AllowStoringTransactionLog_OverrideForStore = _settingService.SettingExists(eigenPaymentSettings, x => x.AllowStoringTransactionLog, storeScope);
                model.TerminalID_OverrideForStore = _settingService.SettingExists(eigenPaymentSettings, x => x.TerminalID, storeScope);
                model.HashPassword_OverrideForStore = _settingService.SettingExists(eigenPaymentSettings, x => x.HashPassword, storeScope);                
            }

            return View("Nop.Plugin.Payments.Eigen.Views.PaymentEigen.Configure", model);
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
            var eigenPaymentSettings = _settingService.LoadSetting<EigenPaymentSettings>(storeScope);

            //save settings
            eigenPaymentSettings.UseSandbox = model.UseSandbox;
            eigenPaymentSettings.AllowStoringTransactionLog = model.AllowStoringTransactionLog;
            eigenPaymentSettings.TerminalID = model.TerminalID;
            eigenPaymentSettings.HashPassword = model.HashPassword;            

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            if (model.UseSandbox_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(eigenPaymentSettings, x => x.UseSandbox, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(eigenPaymentSettings, x => x.UseSandbox, storeScope);

            if (model.AllowStoringTransactionLog_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(eigenPaymentSettings, x => x.AllowStoringTransactionLog, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(eigenPaymentSettings, x => x.AllowStoringTransactionLog, storeScope);

            if (model.TerminalID_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(eigenPaymentSettings, x => x.TerminalID, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(eigenPaymentSettings, x => x.TerminalID, storeScope);

            if (model.HashPassword_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(eigenPaymentSettings, x => x.HashPassword, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(eigenPaymentSettings, x => x.HashPassword, storeScope);            

            //now clear settings cache
            _settingService.ClearCache();

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            //IList<Country> Countries = _countryService.GetAllCountriesForBilling();
            var model = new PaymentInfoModel();
            var flight = _workContext.CurrentCustomer.BillingAddress.Flight;
            //CC types
            model.CreditCardTypes.Add(new SelectListItem()
                {
                    Text = "Visa",
                    Value = "Visa",
                });
            model.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Master card",
                Value = "MasterCard",
            });
            model.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Discover",
                Value = "Discover",
            });
            model.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Amex",
                Value = "Amex",
            });

            //model.Countries = Countries;

            model.Countries.Add(new SelectListItem() { Text = "Select country", Value = "0" });

            foreach (var c in _countryService.GetAllCountries(true))
            {
                model.Countries.Add(new SelectListItem() { Text = c.Name, Value = c.Name });
            }
            
            //years
            for (int i = 0; i < 15; i++)
            {
                string year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem()
                {
                    Text = year,
                    Value = year,
                });
            }

            //months
            for (int i = 1; i <= 12; i++)
            {
                string text = (i < 10) ? "0" + i.ToString() : i.ToString();
                model.ExpireMonths.Add(new SelectListItem()
                {
                    Text = text,
                    Value = i.ToString(),
                });
            }

            DateTime stVoucherDate = new DateTime(2016, 11, 27);
            DateTime enVoucherDate = new DateTime(2018, 7, 1);
            
            if (stVoucherDate <= DateTime.Now && enVoucherDate > DateTime.Now)
            {
                model.IsDisplayVoucher = true;
            }

            //set postback values
            var form = this.Request.Form;
            model.CardholderName = form["CardholderName"];
            model.CardNumber = form["CardNumber"];
            model.CardCode = form["CardCode"];
            //var selectedCcType = model.CreditCardTypes.FirstOrDefault(x => x.Value.Equals(form["CreditCardType"], StringComparison.InvariantCultureIgnoreCase));
            //if (selectedCcType != null)
            //    selectedCcType.Selected = true;
            var selectedMonth = model.ExpireMonths.FirstOrDefault(x => x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedMonth != null)
                selectedMonth.Selected = true;
            var selectedYear = model.ExpireYears.FirstOrDefault(x => x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedYear != null)
                selectedYear.Selected = true;

            model.Email = form["Email"];
            model.PhoneNumber = form["PhoneNumber"];
            model.Address1 = form["Address1"];
            model.City = form["City"];
            model.StateProvinceId = form["StateProvinceId"];

            model.Country = form["Country"];

            model.ZipPostalCode = form["ZipPostalCode"];
            model.Booking = form["Booking"];
            model.AeroplanNumber = form["AeroplanNumber"];
            model.NamePassport = form["NamePassport"];
            model.PassportNumber = form["PassportNumber"];

            model.subscribeYN = form["subscribeYN"] == "Y" ? true : false;

            model.Voucher = form["Voucher"];
            
            //var selectedStateProvince = model.ExpireYears.FirstOrDefault(x => x.Value.Equals(form["StateProvinceId"], StringComparison.InvariantCultureIgnoreCase));
            //if (selectedStateProvince != null)
            //    selectedStateProvince.Selected = true;
            
            return View("Nop.Plugin.Payments.Eigen.Views.PaymentEigen.PaymentInfo", model);
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

            var model = new PaymentInfoModel()
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
                AeroplanNumber = aeroplanNumber,
                Voucher = voucherNum
            };
            var validationResult = validator.Validate(model);

            FluentValidation.Results.ValidationFailure Invalid = null;

            if (!String.IsNullOrEmpty(voucherNum))
            {
                Voucher voucher = _discountService.GetVoucherByNum(voucherNum);

                if (voucher == null)
                {
                    Invalid = new FluentValidation.Results.ValidationFailure("Voucher", _localizationService.GetResource("Payment.Voucher.Invalid"));
                }
                else
                {
                    if (voucher.IsUse)
                    {
                        Invalid = new FluentValidation.Results.ValidationFailure("Voucher", _localizationService.GetResource("Payment.Voucher.Used"));
                    }
                }
            }

            if(Invalid != null)
            {
                validationResult.Errors.Add(Invalid);
            }

            //validationResult.Errors.Add(test)
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    warnings.Add(error.ErrorMessage);
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();

            _workContext.CurrentCustomer.BillingAddress.FirstName = form["NamePassport"];
            _workContext.CurrentCustomer.BillingAddress.Email = form["Email"];
            _workContext.CurrentCustomer.BillingAddress.PhoneNumber = form["PhoneNumber"];
            _workContext.CurrentCustomer.BillingAddress.Address1= form["Address1"];
            _workContext.CurrentCustomer.BillingAddress.State = form["StateProvinceId"];
            _workContext.CurrentCustomer.BillingAddress.CountryN = form["Country"];
            _workContext.CurrentCustomer.BillingAddress.City = form["City"];
            _workContext.CurrentCustomer.BillingAddress.ZipPostalCode = form["ZipPostalCode"];
            _workContext.CurrentCustomer.BillingAddress.Booking = form["Booking"];
            //_workContext.CurrentCustomer.BillingAddress.AeroplanNumber = form["AeroplanNumber"];            
            string aeroplanNumber = form["AeroplanNumber"];
            if(!string.IsNullOrEmpty(aeroplanNumber))
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
            _workContext.CurrentCustomer.BillingAddress.subscribeYN = form["subscribeYN"].Contains("true") ? true:false;
            _workContext.CurrentCustomer.BillingAddress.Voucher = voucherNum;

            _customerService.UpdateCustomer(_workContext.CurrentCustomer);
            paymentInfo.CreditCardType = form["CreditCardType"];
            paymentInfo.CreditCardName = form["CardholderName"];
            paymentInfo.CreditCardNumber = form["CardNumber"];
            paymentInfo.CreditCardExpireMonth = int.Parse(form["ExpireMonth"]);
            paymentInfo.CreditCardExpireYear = int.Parse(form["ExpireYear"]);
            paymentInfo.CreditCardCvv2 = form["CardCode"];

            Flight flightInfo = _flightsService.GetFlightById(Convert.ToInt32(_workContext.CurrentCustomer.BillingAddress.Flight));

            paymentInfo.FlightInfo = "[" + flightInfo.FlightNumber + "]" + _workContext.CurrentCustomer.BillingAddress.Departure.Replace("-", "/");

            return paymentInfo;
        }
    }
}
