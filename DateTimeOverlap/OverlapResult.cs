using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomsCloud.InfrastructureCore.Common.Date
{
    public struct OverlapResult
    {
        public OverlapResult(IEnumerable<OverlapPeriod> overlapPeriods)
        {
            if (overlapPeriods == null) { throw new ArgumentNullException(nameof(overlapPeriods)); }
            HasOverlaps = overlapPeriods.Any();
            OverlapPeriods = overlapPeriods;
        }

        public bool HasOverlaps { get; private set; }

        public IEnumerable<OverlapPeriod> OverlapPeriods { get; private set; }

        public static OverlapResult Default
        {
            get
            {
                return new OverlapResult();
            }
        }
    }
}