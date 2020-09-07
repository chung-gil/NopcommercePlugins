using FluentValidation.Results;
using Nop.Core;
using Nop.Plugin.Payments.IPay;
using Nop.Plugin.Payments.IPay.Models;
using Nop.Plugin.Payments.IPay.Validators;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.IPay.Controllers
{
    public class PaymentIPayController : BaseNopPaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;

        public PaymentIPayController(IWorkContext workContext, IStoreService storeService, ISettingService settingService, ILocalizationService localizationService)
        {          
          this._workContext = workContext;
          this._storeService = storeService;
          this._settingService = settingService;
          this._localizationService = localizationService;
        }

        [ChildActionOnly]
        [AdminAuthorize]
        public ActionResult Configure()
        {
            var scopeConfiguration = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var ipayPaymentSettings = _settingService.LoadSetting<IPayPaymentSettings>(scopeConfiguration);
            
            ConfigurationModel configurationModel = new ConfigurationModel();
            
            configurationModel.TransactModeId = Convert.ToInt32(ipayPaymentSettings.TransactMode);
            configurationModel.CurrencyCodeId = Convert.ToInt32(ipayPaymentSettings.CurrencyCode);

            configurationModel.CompanyKey = ipayPaymentSettings.CompanyKey;
            configurationModel.CompanyKeyAMEX = ipayPaymentSettings.CompanyKeyAMEX;
            configurationModel.TerminalId = ipayPaymentSettings.TerminalId;
            configurationModel.AdditionalFeePercentage = ipayPaymentSettings.AdditionalFeePercentage;
            configurationModel.AdditionalFee = ipayPaymentSettings.AdditionalFee;
            configurationModel.TransactModeValues = ipayPaymentSettings.TransactMode.ToSelectList();
            configurationModel.CurrencyCodeValues = ipayPaymentSettings.CurrencyCode.ToSelectList();
            configurationModel.ActiveStoreScopeConfiguration = scopeConfiguration;

            if (scopeConfiguration > 0)
            {
                configurationModel.TransactModeId_OverrideForStore = _settingService.SettingExists(ipayPaymentSettings, x => x.TransactMode, scopeConfiguration);
                configurationModel.CurrencyCodeId_OverrideForStore = _settingService.SettingExists(ipayPaymentSettings, x => x.CurrencyCode, scopeConfiguration);
                configurationModel.CompanyKey_OverrideForStore = _settingService.SettingExists(ipayPaymentSettings, x => x.CompanyKey, scopeConfiguration);
                configurationModel.CompanyKeyAMEX_OverrideForStore = _settingService.SettingExists(ipayPaymentSettings, x => x.CompanyKeyAMEX, scopeConfiguration);
                configurationModel.TerminalId_OverrideForStore = _settingService.SettingExists(ipayPaymentSettings, x => x.TerminalId, scopeConfiguration);
                configurationModel.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(ipayPaymentSettings, x => x.AdditionalFeePercentage, scopeConfiguration);
                configurationModel.AdditionalFee_OverrideForStore = _settingService.SettingExists(ipayPaymentSettings, x => x.AdditionalFee, scopeConfiguration);
            }

            return View("Nop.Plugin.Payments.IPay.Views.PaymentIPay.Configure", configurationModel);
        }

        [ChildActionOnly]
        [HttpPost]
        [AdminAuthorize]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            int scopeConfiguration = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);

            //save settings
            IPayPaymentSettings ipayPaymentSettings = _settingService.LoadSetting<IPayPaymentSettings>(scopeConfiguration);
            ipayPaymentSettings.TransactMode = (TransactMode) model.TransactModeId;
            ipayPaymentSettings.CurrencyCode = (CurrencyCode) model.CurrencyCodeId;
            ipayPaymentSettings.CompanyKey = model.CompanyKey;
            ipayPaymentSettings.CompanyKeyAMEX = model.CompanyKeyAMEX;
            ipayPaymentSettings.TerminalId = model.TerminalId;
            ipayPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            ipayPaymentSettings.AdditionalFee = model.AdditionalFee;

            if (model.TransactModeId_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.TransactMode, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.TransactMode, scopeConfiguration);
            if (model.CurrencyCodeId_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.CurrencyCode, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.CurrencyCode, scopeConfiguration);
            if (model.CompanyKey_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.CompanyKey, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.CompanyKey, scopeConfiguration);
            if (model.CompanyKeyAMEX_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.CompanyKeyAMEX, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.CompanyKeyAMEX, scopeConfiguration);
            if (model.TerminalId_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.TerminalId, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.TerminalId, scopeConfiguration);
            if (model.TerminalId_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.TerminalId, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.TerminalId, scopeConfiguration);
            if (model.AdditionalFeePercentage_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.AdditionalFeePercentage, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.AdditionalFeePercentage, scopeConfiguration);
            if (model.AdditionalFee_OverrideForStore || scopeConfiguration == 0)
                _settingService.SaveSetting(ipayPaymentSettings, x => x.AdditionalFee, scopeConfiguration, false);
            else if (scopeConfiguration > 0)
                _settingService.DeleteSetting(ipayPaymentSettings, x => x.AdditionalFee, scopeConfiguration);

            //now clear settings cache
            _settingService.ClearCache();

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            PaymentInfoModel paymentInfoModel = new PaymentInfoModel();

            paymentInfoModel.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Visa",
                Value = "Visa"
            });

            paymentInfoModel.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Master card",
                Value = "MasterCard"
            });
            
            paymentInfoModel.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Discover",
                Value = "Discover"
            });

            paymentInfoModel.CreditCardTypes.Add(new SelectListItem()
            {
                Text = "Amex",
                Value = "Amex"
            });

            //years
            for (int index = 0; index < 15; ++index)
            {
                string str = Convert.ToString(DateTime.Now.Year + index);
                paymentInfoModel.ExpireYears.Add(new SelectListItem()
                {
                    Text = str,
                    Value = str
                });
            }

            //months
            for (int index = 1; index <= 12; ++index)
            {
                string str = index < 10 ? "0" + index.ToString() : index.ToString();
                paymentInfoModel.ExpireMonths.Add(new SelectListItem()
                {
                    Text = str,
                    Value = index.ToString()
                });
            }

            //set postback values
            var form = this.Request.Form;
            paymentInfoModel.CardholderName = form["CardholderName"];
            paymentInfoModel.CardNumber = form["CardNumber"];
            paymentInfoModel.CardCode = form["CardCode"];
            
            var selectListItem1 = paymentInfoModel.CreditCardTypes.FirstOrDefault(x => x.Value.Equals(form["CreditCardType"], StringComparison.InvariantCultureIgnoreCase));
            if (selectListItem1 != null)
            selectListItem1.Selected = true;
            var selectListItem2 = paymentInfoModel.ExpireMonths.FirstOrDefault(x => x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));
            if (selectListItem2 != null)
            selectListItem2.Selected = true;
            var selectListItem3 = paymentInfoModel.ExpireYears.FirstOrDefault(x => x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));
            if (selectListItem3 != null)
            selectListItem3.Selected = true;
            return View("Nop.Plugin.Payments.IPay.Views.PaymentIPay.PaymentInfo", paymentInfoModel);                         
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel()
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    warnings.Add(error.ErrorMessage);
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var processPaymentRequest = new ProcessPaymentRequest();
            processPaymentRequest.CreditCardType = form["CreditCardType"];
            processPaymentRequest.CreditCardName = form["CardholderName"];
            processPaymentRequest.CreditCardNumber = form["CardNumber"];
            processPaymentRequest.CreditCardExpireMonth = int.Parse(form["ExpireMonth"]);
            processPaymentRequest.CreditCardExpireYear = int.Parse(form["ExpireYear"]);
            processPaymentRequest.CreditCardCvv2 = form["CardCode"];
            return processPaymentRequest;
        }
    }
}
