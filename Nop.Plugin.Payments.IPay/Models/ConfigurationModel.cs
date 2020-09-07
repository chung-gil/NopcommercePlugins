using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;
using System;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.IPay.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.IPay.Fields.TerminalId")]
        public string TerminalId { get; set; }

        public bool TerminalId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.IPay.Fields.CompanyKey")]
        public string CompanyKey { get; set; }

        public bool CompanyKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.IPay.Fields.CompanyKeyAMEX")]
        public string CompanyKeyAMEX { get; set; }

        public bool CompanyKeyAMEX_OverrideForStore { get; set; }

        public int TransactModeId { get; set; }

        public bool TransactModeId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.IPay.Fields.TransactModeValues")]
        public SelectList TransactModeValues { get; set; }

        public int CurrencyCodeId { get; set; }

        public bool CurrencyCodeId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.IPay.Fields.CurrencyCode")]
        public SelectList CurrencyCodeValues { get; set; }

        [NopResourceDisplayName("Plugins.Payments.IPay.Fields.AdditionalFee")]
        public Decimal AdditionalFee { get; set; }

        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.IPay.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }

        public bool AdditionalFeePercentage_OverrideForStore { get; set; }
    }
}
