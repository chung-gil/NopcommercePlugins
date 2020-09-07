using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Eigen.Models
{
    public class ConfigurationModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Eigen.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Eigen.Fields.AllowStoringTransactionLog")]
        public bool AllowStoringTransactionLog { get; set; }
        public bool AllowStoringTransactionLog_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Eigen.Fields.TerminalID")]
        public string TerminalID { get; set; }
        public bool TerminalID_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Eigen.Fields.HashPassword")]
        public string HashPassword { get; set; }
        public bool HashPassword_OverrideForStore { get; set; }
    }
}
