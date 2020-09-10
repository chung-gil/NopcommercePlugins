using System;
using System.Collections.Generic;
//using System.Collections.Specialized;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Web.Routing;
using System.Linq;
using System.Security.Cryptography;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Eigen.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Mustache;

using System.IO;
using System.Net;

namespace Nop.Plugin.Payments.Eigen
{
    /// <summary>
    /// Eigen payment processor
    /// </summary>
    public class EigenPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly EigenPaymentSettings _eigenPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IEncryptionService _encryptionService;
        private readonly IOrderService _orderService;

        #endregion

        #region Ctor

        public EigenPaymentProcessor(EigenPaymentSettings eigenPaymentSettings,
            ISettingService settingService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            IOrderTotalCalculationService orderTotalCalculationService, IEncryptionService encryptionService,
            IOrderService orderService)
        {
            this._eigenPaymentSettings = eigenPaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._encryptionService = encryptionService;
            this._orderService = orderService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Create Sha1 HashHex string
        /// </summary>
        /// <returns></returns>
        private string CreateSha1HashHexString(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);

            using (var sha1 = SHA1.Create())
            {
                byte[] hashBytes = sha1.ComputeHash(bytes);
                return GetHexStringFromBytes(hashBytes);
            }
        }

        /// <summary>
        /// Gets Hexstring from bytes
        /// </summary>
        /// <returns></returns>
        private string GetHexStringFromBytes(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (var hex in bytes.Select(b => b.ToString("x2")))
            {
                sb.Append(hex);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets Eigen URL
        /// </summary>
        /// <returns></returns>
        private string GetEigenUrl()
        {
            return _eigenPaymentSettings.UseSandbox ? "https://staging.eigendev.com/OFT/EigenOFT_p.php" : "https://ms1.eigendev.com/OFT/EigenOFT_p.php";
            //return "https://ms1.eigendev.com/OFT/EigenOFT_p.php";
        }
        
        /// <summary>
        ///  Get errors (ARB Support)
        /// </summary>
        /// <param name="response"></param>
        private Dictionary<string, string> GetResponseCodes(string response)
        {
            return response.Split(',').ToDictionary(item => item.Substring(0, 2), item => item.Substring(2));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            var compiler = new FormatCompiler();

            // build the track 2 string 

            const string track2Template = "M{{Pan}}={{ExpiryYear:00}}{{ExpiryMonth:00}}0?";

            var track2Data = new
            {
                Pan = processPaymentRequest.CreditCardNumber,
                ExpiryYear = processPaymentRequest.CreditCardExpireYear - 2000,
                ExpiryMonth = processPaymentRequest.CreditCardExpireMonth.ToString("D2")
            };

            //var track2String = compiler.format(track2Template, track2Data);
            Generator generator = compiler.Compile(track2Template);
            var track2String = generator.Render(track2Data);

            // build the MKey string

            const string mkeyTemplate = "{{MkeyPassword}}{{TerminalId}}27{{Amount}}{{SubmissionDate:yyyyMMddHHmmss}}{{Track2}}";

            //var reference = (_eigenPaymentSettings.UseSandbox ? "TEST" : "") + processPaymentRequest.OrderGuid.ToString();
            //var reference = processPaymentRequest.OrderGuid.ToString();
            var reference = _orderService.GetMaxOrderNumber().ToString();
            var flightInfo = processPaymentRequest.FlightInfo;

            //submission.DateSubmitted = DateTime.UtcNow;
            var orderTotal = Math.Round(processPaymentRequest.OrderTotal, 2);

            var eigenData = new
            {
                TerminalId = _eigenPaymentSettings.TerminalID,
                MkeyPassword = _eigenPaymentSettings.HashPassword,
                Track2 = track2String,
                Amount = Convert.ToInt32(orderTotal * 100M),
                SubmissionDate = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Eastern Standard Time"),
                Reference = reference,
                FlightInfo = flightInfo,
                Mkey = new StringBuilder()
            };

            //var mKeyPlainString = compiler.Format(mkeyTemplate, eigenData);
            generator = compiler.Compile(mkeyTemplate);
            var mKeyPlainString = generator.Render(eigenData);

            // build the request URL with query string

            eigenData.Mkey.Append(CreateSha1HashHexString(mKeyPlainString));

            const string eigenQueryTemplate = "MTQ,TG{{TerminalId}},TC27,T2{{Track2}},A1{{Amount}},ED{{FlightInfo}},IN{{Reference}},DT{{SubmissionDate:yyyyMMddHHmmss}},MY{{Mkey}}";

            generator = compiler.Compile(eigenQueryTemplate);
            var querystring = generator.Render(eigenData);

            var uriBuilder = new UriBuilder(GetEigenUrl())
            {
                Query = querystring
            };

            // submit the transaction to Eigen

            Dictionary<string, string> responseCodes = null;

            try
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(uriBuilder.Uri).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = response.Content.ReadAsStringAsync().Result;
                        responseCodes = GetResponseCodes(responseString);
                    }
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }

            if (responseCodes == null || !responseCodes.Any())
            {
                result.AddError("Eigen unknown error");
            }

            switch (responseCodes.GetValueOrDefault("AB"))
            {
                case "Y":
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    break;
                default:
                    result.AddError(string.Format("Declined ({0})", responseCodes.GetValueOrDefault("RM")));
                    break;
            }

            result.AuthorizationTransactionResult = responseCodes.GetValueOrDefault("RM");
            result.AuthorizationTransactionCode = responseCodes.GetValueOrDefault("IN");
            result.AuthorizationTransactionId = responseCodes.GetValueOrDefault("AC");
            result.AllowStoringCreditCardNumber = _eigenPaymentSettings.AllowStoringTransactionLog;


            foreach (var responseCode in responseCodes)
            {
                if (responseCode.Key == "PA" || responseCode.Key == "T2") continue;
                result.ResponseCodes.Add(responseCode.Key, responseCode.Value);
                //submission.ResponseCodes.Add(responseCode.Key, responseCode.Value);
            }

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //nothing
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");       

            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();        
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //it's not a redirection payment method. So we always return false
            return false;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentEigen";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Eigen.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentEigen";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Eigen.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentEigenController);
        }

        public override void Install()
        {
            //settings
            var settings = new EigenPaymentSettings()
            {
                UseSandbox = true,
                TerminalID = "",
                HashPassword = ""
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Notes", "If you're using this gateway, ensure that your primary store currency is supported by Eigen.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.AllowStoringTransactionLog", "Store Transaction Log");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.AllowStoringTransactionLog.Hint", "Store Transaction Log in Database");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.TerminalID", "Terminal ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.TerminalID.Hint", "Terminal ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.HashPassword", "Hash Password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Eigen.Fields.HashPassword.Hint", "HashPassword");

            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<EigenPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Notes");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.AllowStoringTransactionLog");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.AllowStoringTransactionLog.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.TerminalID");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.TerminalID.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.HashPassword");
            this.DeletePluginLocaleResource("Plugins.Payments.Eigen.Fields.HashPassword.Hint");

            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.Manual;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        #endregion
    }
}
