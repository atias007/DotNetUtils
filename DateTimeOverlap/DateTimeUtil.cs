using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CustomsCloud.InfrastructureCore.Common.Date
{
    public static class DateTimeUtil
    {
        public static OverlapResult IsOverlap(IEnumerable<DateTimePeriod> periods)
        {
            return IsOverlap(periods.ToArray());
        }

        public static OverlapResult IsOverlap(params DateTimePeriod[] periods)
        {
            if (periods == null) { return OverlapResult.Default; }
            if (periods.Length < 2) { return OverlapResult.Default; }
            var arrPeriods = periods.ToArray();

            var list = new List<OverlapPeriod>();
            for (int i = 0; i < arrPeriods.Length; i++)
            {
                for (int j = i + 1; j < arrPeriods.Length; j++)
                {
                    if (IsOverlap(arrPeriods[i], arrPeriods[j]))
                    {
                        var overlap = new OverlapPeriod(arrPeriods[i], arrPeriods[j]);
                        list.Add(overlap);
                    }
                }
            }

            var result = new OverlapResult(list);
            return result;
        }

        private static bool IsOverlap(DateTimePeriod period1, DateTimePeriod period2)
        {
            if (Between(period1, period2)) { return true; }
            if (Between(period2, period1)) { return true; }
            if (period1.Equals(period2)) { return true; }
            return false;
        }

        private static bool Between(DateTimePeriod period1, DateTimePeriod period2)
        {
            if (Between(period1.From, period2)) { return true; }
            if (Between(period1.To, period2)) { return true; }
            return false;
        }

        private static bool Between(DateTime date, DateTimePeriod period)
        {
            return date > period.From && date < period.To;
        }
    }
}