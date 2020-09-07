using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.IPay
{
    public enum TransactMode
    {
        Authorize = 1,
        AuthorizeAndCapture = 2,
    }
}
