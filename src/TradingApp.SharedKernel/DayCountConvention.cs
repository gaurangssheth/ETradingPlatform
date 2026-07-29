using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.SharedKernel
{
    public enum DayCountConvention
    {
        ActualActual = 1,
        Actual365 = 2,
        Actual360 = 3,
        Thirty360 = 4
    }
}
