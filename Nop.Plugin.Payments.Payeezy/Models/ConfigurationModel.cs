using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Payeezy.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Payeezy.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Payeezy.Fields.ExactID")]
        public string ExactID { get; set; }
        public bool ExactID_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Payeezy.Fields.Password")]
        public string Password { get; set; }
        public bool Password_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Payeezy.Fields.keyID")]
        public string keyID { get; set; }
        public bool keyID_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Payeezy.Fields.Hmackey")]
        public string Hmackey { get; set; }
        public bool Hmackey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Payeezy.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Payeezy.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }
    }
}
