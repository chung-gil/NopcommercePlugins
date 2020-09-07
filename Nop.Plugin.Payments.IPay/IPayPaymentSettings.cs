using Nop.Core.Configuration;
using System;

namespace Nop.Plugin.Payments.IPay
{
    public class IPayPaymentSettings : ISettings
    {
        public string TerminalId { get; set; }

        public string CompanyKey { get; set; }

        public string CompanyKeyAMEX { get; set; }

        public TransactMode TransactMode { get; set; }

        public CurrencyCode CurrencyCode { get; set; }

        public bool AdditionalFeePercentage { get; set; }

        public Decimal AdditionalFee { get; set; }
    }
}
