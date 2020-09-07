using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Routing;
using System.Xml;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Payeezy.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;

namespace Nop.Plugin.Payments.Payeezy
{
    /// <summary>
    /// Payeezy payment processor
    /// </summary>
    public class PayeezyPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger _logger;
        private readonly CurrencySettings _currencySettings;
        private readonly PayeezyPaymentSettings _payeezyPaymentSettings;
        #endregion

        #region Ctor

        public PayeezyPaymentProcessor(ISettingService settingService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IWebHelper webHelper,
            IOrderTotalCalculationService orderTotalCalculationService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IEncryptionService encryptionService,
            ILogger logger,
            CurrencySettings currencySettings,
            PayeezyPaymentSettings payeezyPaymentSettings)
        {
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._webHelper = webHelper;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._encryptionService = encryptionService;
            this._logger = logger;
            this._currencySettings = currencySettings;
            this._payeezyPaymentSettings = payeezyPaymentSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets Eigen URL
        /// </summary>
        /// <returns></returns>
        private string GetPaymentUrl()
        {
            return _payeezyPaymentSettings.UseSandbox ? "https://api.demo.globalgatewaye4.firstdata.com" : "https://api.globalgatewaye4.firstdata.com";
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
            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);

            StringBuilder string_builder = new StringBuilder();
            using (StringWriter string_writer = new StringWriter(string_builder))
            {
                using (XmlTextWriter xml_writer = new XmlTextWriter(string_writer))
                {     //build XML string 
                    xml_writer.Formatting = Formatting.Indented;
                    xml_writer.WriteStartElement("Transaction");
                    xml_writer.WriteElementString("ExactID", _payeezyPaymentSettings.ExactID);//Gateway ID
                    xml_writer.WriteElementString("Password", _payeezyPaymentSettings.Password);//Password
                    xml_writer.WriteElementString("Transaction_Type", "00");
                    xml_writer.WriteElementString("DollarAmount", Math.Round(processPaymentRequest.OrderTotal, 2).ToString("0.00"));
                    xml_writer.WriteElementString("Expiry_Date", processPaymentRequest.CreditCardExpireMonth.ToString("D2") + processPaymentRequest.CreditCardExpireYear.ToString().Substring(processPaymentRequest.CreditCardExpireYear.ToString().Length-2, 2));
                    xml_writer.WriteElementString("CardHoldersName", customer.BillingAddress.FirstName);
                    xml_writer.WriteElementString("Card_Number", processPaymentRequest.CreditCardNumber);
                    xml_writer.WriteEndElement();
                }
            }
            string xml_string = string_builder.ToString();

            //SHA1 hash on XML string
            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] xml_byte = encoder.GetBytes(xml_string);
            SHA1CryptoServiceProvider sha1_crypto = new SHA1CryptoServiceProvider();
            string hash = BitConverter.ToString(sha1_crypto.ComputeHash(xml_byte)).Replace("-", "");
            string hashed_content = hash.ToLower();

            //assign values to hashing and header variables
            string keyID = _payeezyPaymentSettings.keyID;//key ID
            string Hmackey = _payeezyPaymentSettings.Hmackey;//Hmac key
            string method = "POST\n";
            string type = "application/xml";//REST XML
            string time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string uri = "/transaction/v13";
            string hash_data = method + type + "\n" + hashed_content + "\n" + time + "\n" + uri;

            //hmac sha1 hash with key + hash_data
            HMAC hmac_sha1 = new HMACSHA1(Encoding.UTF8.GetBytes(Hmackey)); //key
            byte[] hmac_data = hmac_sha1.ComputeHash(Encoding.UTF8.GetBytes(hash_data)); //data
                                                                                         //base64 encode on hmac_data
            string base64_hash = Convert.ToBase64String(hmac_data);

            string url = GetPaymentUrl() + uri; //Payment Url Endpoint
                        
            //begin HttpWebRequest 
            HttpWebRequest web_request = (HttpWebRequest)WebRequest.Create(url);
            web_request.Method = "POST";
            web_request.ContentType = type;
            web_request.Accept = "*/*";
            web_request.Headers.Add("x-gge4-date", time);
            web_request.Headers.Add("x-gge4-content-sha1", hashed_content);
            web_request.Headers.Add("Authorization", "GGE4_API " + keyID + ":" + base64_hash);
            web_request.ContentLength = xml_string.Length;
            web_request.KeepAlive = false;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // write and send request data 
            using (StreamWriter stream_writer = new StreamWriter(web_request.GetRequestStream()))
            {
                stream_writer.Write(xml_string);
            }

            //get response and read into string
            string response_string;
            try
            {
                using (HttpWebResponse web_response = (HttpWebResponse)web_request.GetResponse())
                {
                    using (StreamReader response_stream = new StreamReader(web_response.GetResponseStream()))
                    {
                        response_string = response_stream.ReadToEnd();
                    }

                    //load xml
                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.LoadXml(response_string);
                    XmlNodeList nodelist = xmldoc.SelectNodes("TransactionResult");

                    //fail
                    if(Convert.ToBoolean(nodelist.Item(0).SelectSingleNode("Transaction_Error").InnerText))
                    {
                        result.AddError(nodelist.Item(0).SelectSingleNode("EXact_Message").InnerText);
                    }
                    else
                    {
                        if (Convert.ToBoolean(nodelist.Item(0).SelectSingleNode("Transaction_Approved").InnerText))
                        {
                            result.AuthorizationTransactionId = nodelist.Item(0).SelectSingleNode("SequenceNo").InnerText;
                            result.AuthorizationTransactionCode = nodelist.Item(0).SelectSingleNode("Authorization_Num").InnerText;
                            result.AuthorizationTransactionResult = nodelist.Item(0).SelectSingleNode("Bank_Message").InnerText;
                        }
                        else
                        {
                            result.AddError(nodelist.Item(0).SelectSingleNode("EXact_Message").InnerText);
                        }
                    }
                }
            }
            //read stream for remote error response
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (HttpWebResponse error_response = (HttpWebResponse)ex.Response)
                    {
                        using (StreamReader reader = new StreamReader(error_response.GetResponseStream()))
                        {
                            string remote_ex = reader.ReadToEnd();
                            //error.Text = remote_ex;
                            result.AddError(remote_ex);                            
                        }
                    }
                }
                else
                {
                    throw;
                }
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
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart, 0m, false);
            return result;
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();

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
        /// Process recurring payment
        /// </summary>
        /// <param name="transactionId">AuthorizeNet transaction ID</param>
        public void ProcessRecurringPayment(string transactionId)
        {
            
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
            controllerName = "PaymentPayeezy";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Payeezy.Controllers" }, { "area", null } };
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
            controllerName = "PaymentPayeezy";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Payeezy.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentPayeezyController);
        }

        public override void Install()
        {
            //settings
            var settings = new PayeezyPaymentSettings
            {
                UseSandbox = true,
                ExactID = "",
                Password = "",
                keyID = "",
                Hmackey = ""
            };

            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Notes", "If you're using this gateway, ensure that your ExactID, Password, keyID, Hmackey.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.ExactID", "ExactID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.ExactID.Hint", "ExactID (Gateway ID).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Password", "Password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Password.Hint", "Specify Password.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.keyID", "keyID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.keyID.Hint", "Specify keyID.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Hmackey", "Hmackey");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Hmackey.Hint", "Specify Hmackey.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");

            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayeezyPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Notes");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.ExactID");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.ExactID.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Password");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Password.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.keyID");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.keyID.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Hmackey");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.Hmackey.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.Payeezy.Fields.AdditionalFeePercentage.Hint");

            base.Uninstall();
        }

        #endregion

        #region Properties

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
                return RecurringPaymentType.Automatic;
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
