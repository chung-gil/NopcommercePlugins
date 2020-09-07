using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Payeezy
{
    public class PayeezyPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }        
        public string ExactID { get; set; }
        public string Password { get; set; }
        public string keyID { get; set; }
        public string Hmackey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }

        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
    }
}
