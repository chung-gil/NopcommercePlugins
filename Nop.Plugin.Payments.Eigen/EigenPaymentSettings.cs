using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Eigen
{
    public class EigenPaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public bool AllowStoringTransactionLog { get; set; }
        public string TerminalID { get; set; }
        public string HashPassword { get; set; }
    }
}