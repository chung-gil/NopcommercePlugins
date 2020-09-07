using Enterprise.XTrans;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.IPay.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Routing;
using System.Xml.Serialization;

namespace Nop.Plugin.Payments.IPay
{
    public class IPayPaymentProcessor : BasePlugin, IPaymentMethod, IPlugin
    {
        private const string TRANSACTION_INDICATOR = "7";
        private const string SERVICE = "CC";
        private const string SERVICE_TYPE = "DEBIT";
        private const string SERVICE_FORMAT = "1010";
        private readonly IPayPaymentSettings _iPaySettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;

        public bool SupportCapture
        {
            get
            {
                return true;
            }
        }

        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.Manual;
            }
        }

        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        public IPayPaymentProcessor(ISettingService _settingService, ICurrencyService _currencyService, ICustomerService _customerService, CurrencySettings _currencySettings, IWebHelper _webHelper, IOrderTotalCalculationService _orderTotalCalculationService, IPayPaymentSettings _iPaySettings)
        {          
            this._settingService = _settingService;
            this._currencyService = _currencyService;
            this._customerService = _customerService;
            this._currencySettings = _currencySettings;
            this._webHelper = _webHelper;
            this._orderTotalCalculationService = _orderTotalCalculationService;
            this._iPaySettings = _iPaySettings;
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            ProcessPaymentResult result = new ProcessPaymentResult();
            
            string CompanyKey = this._iPaySettings.CompanyKey;

            if (processPaymentRequest.CreditCardType.Equals("Amex"))
                CompanyKey = this._iPaySettings.CompanyKeyAMEX;

            Processor processor1 = new Processor("prd.txngw.com", 86, CompanyKey, 30000);
            processor1.EncryptionMode = EncryptionModeType.TripleDES;
            processor1.AddEncryptionKey("0502114302159200");
            processor1.AddEncryptionKey("0029512034112050");
            processor1.AddEncryptionKey("0215920005021143");
            processor1.SetValue("TRANSACTION_INDICATOR", "7");
            processor1.SetValue("SERVICE", "CC");
            processor1.SetValue("SERVICE_TYPE", "DEBIT");
            processor1.SetValue("SERVICE_SUBTYPE", "AUTH");
            processor1.SetValue("SERVICE_FORMAT", "1010");
            
            Customer customerById = this._customerService.GetCustomerById(processPaymentRequest.CustomerId);
            processor1.SetValue("ACCOUNT_NUMBER", processPaymentRequest.CreditCardNumber);
            processor1.SetValue("FIRST_NAME", customerById.BillingAddress.FirstName);
            processor1.SetValue("LAST_NAME", customerById.BillingAddress.LastName);
            processor1.SetValue("ADDRESS", customerById.BillingAddress.Address1);
            processor1.SetValue("CITY", customerById.BillingAddress.City);
            processor1.SetValue("POSTAL_CODE", customerById.BillingAddress.ZipPostalCode);
            processor1.SetValue("CURRENCY_CODE", Convert.ToInt32(this._iPaySettings.CurrencyCode).ToString());
            processor1.SetValue("CURRENCY_INDICATOR", "1");
            processor1.SetValue("CVV", processPaymentRequest.CreditCardCvv2);
            processor1.SetValue("COUNTRY", customerById.BillingAddress.Country.ThreeLetterIsoCode);
            
            Processor processor2 = processor1;
            string KeyName = "EXPIRATION";
            int num1 = processPaymentRequest.CreditCardExpireMonth;
            string str1 = num1.ToString("D2");
            num1 = processPaymentRequest.CreditCardExpireYear;
            string str2 = num1.ToString().Substring(2, 2);
            string KeyValue = str1 + str2;
            processor2.SetValue(KeyName, KeyValue);
            processor1.SetValue("TERMINAL_ID", this._iPaySettings.TerminalId.ToString());
            Decimal num2 = Math.Round(processPaymentRequest.OrderTotal, 2);
            processor1.SetValue("AMOUNT", num2.ToString("0.00", (IFormatProvider) CultureInfo.InvariantCulture));
            processor1.Build();
            processor1.ProcessRequest();
            string xmlValue1 = this.GetXMLValue(processor1.ResponseXml, "RESPONSE_TEXT");
            string xmlValue2 = this.GetXMLValue(processor1.ResponseXml, "MRC");
            string xmlValue3 = this.GetXMLValue(processor1.ResponseXml, "ARC");
            if (xmlValue3 == "00" && xmlValue2 == "00")
            {
                if (xmlValue1 == "APPROVAL" || xmlValue1 == "Approved")
                {
                    result.AuthorizationTransactionCode = this.GetXMLValue(processor1.ResponseXml, "APPROVAL_CODE");
                    result.AuthorizationTransactionId = this.GetXMLValue(processor1.ResponseXml, "TRANSACTION_ID");
                    result.AuthorizationTransactionResult = xmlValue1;
                    result.NewPaymentStatus = PaymentStatus.Authorized;
                    if (this.GetTransactionMode() == "CAPTURE")
                    {
                        processor1.ResetObjects();
                        // to prevent error : TRAN NOT FOUND
                        // delay 5 seconds
                        System.Threading.Thread.Sleep(5000);
                        result = this.CaptureIPay(result.AuthorizationTransactionId, num2.ToString("0.00", (IFormatProvider)CultureInfo.InvariantCulture), result, processPaymentRequest.CreditCardType);
                    }
                }
                else
                {
                    result.AddError(string.Format("Error: APPROVAL : {0}:ARC{1}:MRC{2}", (object)xmlValue1, (object)xmlValue3, (object)xmlValue2));
                }
            }
            else
            {
                processor1.ResetObjects();
                result.AddError(string.Format("Error: ProcessPayment : {0}:ARC{1}:MRC{2}", (object)xmlValue1, (object)xmlValue3, (object)xmlValue2));
            }
            return result;
        }

        public ProcessPaymentResult CaptureIPay(string transactionId, string amount, ProcessPaymentResult result, string creditCardType)
        {
            string CompanyKey = this._iPaySettings.CompanyKey;
            if (creditCardType.Equals("Amex"))
            CompanyKey = this._iPaySettings.CompanyKeyAMEX;
            Processor processor = new Processor("prd.txngw.com", 86, CompanyKey, 30000);
            processor.EncryptionMode = EncryptionModeType.TripleDES;
            processor.AddEncryptionKey("0502114302159200");
            processor.AddEncryptionKey("0029512034112050");
            processor.AddEncryptionKey("0215920005021143");
            processor.SetValue("TERMINAL_ID", this._iPaySettings.TerminalId.ToString());
            processor.SetValue("SERVICE_FORMAT", "1010");
            processor.SetValue("CURRENCY_CODE", Convert.ToInt32(this._iPaySettings.CurrencyCode).ToString());
            processor.SetValue("CURRENCY_INDICATOR", "1");
            processor.SetValue("TRANSACTION_ID", transactionId);
            processor.SetValue("SERVICE", "CC");
            processor.SetValue("SERVICE_TYPE", "DEBIT");
            processor.SetValue("SERVICE_SUBTYPE", "CAPTURE");
            processor.SetValue("AMOUNT", amount);
            processor.Build();
            processor.ProcessRequest();
            string xmlValue1 = this.GetXMLValue(processor.ResponseXml, "RESPONSE_TEXT");
            string xmlValue2 = this.GetXMLValue(processor.ResponseXml, "MRC");
            string xmlValue3 = this.GetXMLValue(processor.ResponseXml, "ARC");
            if (xmlValue3 == "00" && xmlValue2 == "00")
            {
                if (xmlValue1 == "TRAN CAPTURED")
                {
                    result.CaptureTransactionId = this.GetXMLValue(processor.ResponseXml, "APPROVAL_CODE");
                    result.CaptureTransactionResult = xmlValue1;
                    result.NewPaymentStatus = PaymentStatus.Paid;
                }
                else
                {
                    result.AddError(string.Format("Error: TRAN CAPTURED : {0}:ARC{1}:MRC{2}", (object)xmlValue1, (object)xmlValue3, (object)xmlValue2));
                }
            }
            else
            {
                result.AddError(string.Format("Error: CaptureIPay : {0}:ARC{1}:MRC{2}", (object)xmlValue1, (object)xmlValue3, (object)xmlValue2));
            }
            processor.ResetObjects();
            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
        }

        public Decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return PaymentExtentions.CalculateAdditionalFee((IPaymentMethod) this, this._orderTotalCalculationService, cart, this._iPaySettings.AdditionalFee, this._iPaySettings.AdditionalFeePercentage);
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            CapturePaymentResult capturePaymentResult = new CapturePaymentResult();
            Processor processor = new Processor("prd.txngw.com", 86, this._iPaySettings.CompanyKey, 30000);
            processor.EncryptionMode = EncryptionModeType.TripleDES;
            processor.AddEncryptionKey("0502114302159200");
            processor.AddEncryptionKey("0029512034112050");
            processor.AddEncryptionKey("0215920005021143");
            processor.SetValue("TERMINAL_ID", this._iPaySettings.TerminalId.ToString());
            processor.SetValue("SERVICE_FORMAT", "1010");
            processor.SetValue("CURRENCY_CODE", Convert.ToInt32(this._iPaySettings.CurrencyCode).ToString());
            processor.SetValue("CURRENCY_INDICATOR", "1");
            processor.SetValue("TRANSACTION_ID", capturePaymentRequest.Order.AuthorizationTransactionId);
            processor.SetValue("SERVICE", "CC");
            processor.SetValue("SERVICE_TYPE", "DEBIT");
            processor.SetValue("SERVICE_SUBTYPE", "CAPTURE");
            processor.SetValue("AMOUNT", capturePaymentRequest.Order.OrderTotal.ToString("0.00", (IFormatProvider) CultureInfo.InvariantCulture));
            processor.Build();
            processor.ProcessRequest();
            string xmlValue1 = this.GetXMLValue(processor.ResponseXml, "RESPONSE_TEXT");
            string xmlValue2 = this.GetXMLValue(processor.ResponseXml, "MRC");
            string xmlValue3 = this.GetXMLValue(processor.ResponseXml, "ARC");
            if (xmlValue3 == "00" && xmlValue2 == "00")
            {
                if (xmlValue1 == "TRAN CAPTURED")
                {
                    capturePaymentResult.CaptureTransactionId = this.GetXMLValue(processor.ResponseXml, "APPROVAL_CODE");
                    capturePaymentResult.CaptureTransactionResult = xmlValue1;
                    capturePaymentResult.NewPaymentStatus = PaymentStatus.Paid;
                }
                else
                {
                    capturePaymentResult.AddError(string.Format("Error: {0}:ARC{1}:MRC{2}", xmlValue1, xmlValue3, xmlValue2));
                }
            }
            else
            {
                capturePaymentResult.AddError(string.Format("Error: {0}:ARC{1}:MRC{2}", xmlValue1, xmlValue3, xmlValue2));
            }

            processor.ResetObjects();

            return capturePaymentResult;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
          throw new NotImplementedException();
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");
            return false;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentIPay";

            routeValues = new RouteValueDictionary()
            {
                {
                  "Namespaces",
                  "Nop.Plugin.Payments.IPay.Controllers"
                },
                {
                  "area",
                  null
                }
            };
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentIPay";

            routeValues = new RouteValueDictionary()
            {
                {
                  "Namespaces",
                  "Nop.Plugin.Payments.IPay.Controllers"
                },
                {
                  "area",
                  null
                }
            };
        }

        public override void Install()
        {
            this._settingService.SaveSetting(new IPayPaymentSettings()
            {
                TerminalId = "6177",
                TransactMode = TransactMode.Authorize,
                CompanyKey = "8993",
                CurrencyCode = CurrencyCode.USD,
                AdditionalFee = new Decimal(0),
                AdditionalFeePercentage = false
              });

              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.TerminalId", "Terminal Id");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.TerminalId.Hint", "Terminal Id");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.TransactModeValues", "Transaction mode");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.TransactModeValues.Hint", "Choose transaction mode");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.CompanyKey", "Company key");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.CompanyKey.Hint", "Specify company key");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.CompanyKey", "Company key for AMEX");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.CompanyKey.Hint", "Specify company key for transactions using AMEX credit card");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.CurrencyCode", "Default Currency");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.CurrencyCode.Hint", "Specify default currency.");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.AdditionalFee", "Additional fee");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
              this.AddOrUpdatePluginLocaleResource("Plugins.Payments.IPay.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");

              base.Install();
        }

        public override void Uninstall()
        {
            this._settingService.DeleteSetting<IPayPaymentSettings>();
            this.DeletePluginLocaleResource("Plugins.Payments.IPay.Fields.TerminalId");
            this.DeletePluginLocaleResource("Plugins.Payments.IPay.Fields.TerminalId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.IPay.Fields.TransactModeValues");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.TransactModeValues.Hint");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.CompanyKey");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.CompanyKey.Hint");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.CompanyKeyAMEX");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.CompanyKeyAMEX.Hint");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.CurrencyCode");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.CurrencyCode.Hint");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.AdditionalFee");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource( "Plugins.Payments.IPay.Fields.AdditionalFeePercentage.Hint");
          
            base.Uninstall();
        }

        public Type GetControllerType()
        {
          return typeof (PaymentIPayController);
        }

        private IPayPaymentProcessor.PaymentResponseField[] GetXMLValues(string xmlText)
        {
            IPayPaymentProcessor.PaymentResponse paymentResponse;

            using (StringReader stringReader = new StringReader(xmlText))
            {
                paymentResponse = (IPayPaymentProcessor.PaymentResponse)new XmlSerializer(typeof(IPayPaymentProcessor.PaymentResponse)).Deserialize((TextReader)stringReader);
            }
            return paymentResponse.Fields;
        }

        private string GetXMLValue(string xmlText, string key)
        {
            foreach (IPayPaymentProcessor.PaymentResponseField paymentResponseField in this.GetXMLValues(xmlText))
            {
                if (paymentResponseField.Key == key)
                    return paymentResponseField.Value;
            }
            return string.Empty;
        }

        private string GetTransactionMode()
        {
            switch (Convert.ToInt32((object) this._iPaySettings.TransactMode))
            {
                case 1:
                    return "AUTH";
                case 2:
                    return "CAPTURE";
                default:
                    return "AUTH";
            }
        }

        [XmlRoot("RESPONSE")]
        public class PaymentResponse
        {
            [XmlArrayItem("FIELD")]
            [XmlArray("FIELDS")]
            public IPayPaymentProcessor.PaymentResponseField[] Fields { get; set; }
        }

        public class PaymentResponseField
        {
            [XmlAttribute("KEY")]
            public string Key { get; set; }

            [XmlText]
            public string Value { get; set; }
        }
    }
}
