using System;
using System.Diagnostics.CodeAnalysis;

namespace CustomsCloud.InfrastructureCore.Common.Date
{
    public struct DateTimePeriod
    {
        public DateTimePeriod(DateTime from, DateTime to)
        {
            From = from;
            To = to;
        }

        public DateTimePeriod(DateOnly from, DateOnly to)
        {
            var time = new TimeOnly();
            From = from.ToDateTime(time);
            To = to.ToDateTime(time);
        }

        public DateTimePeriod(TimeOnly from, TimeOnly to)
            : this(DateTime.MinValue + from.ToTimeSpan(), DateTime.MinValue + to.ToTimeSpan())
        {
        }

        public DateTimePeriod(TimeSpan from, TimeSpan to)
            : this(DateTime.MinValue + from, DateTime.MinValue + to)
        {
        }

        public DateTime From { get; private set; }
        public DateTime To { get; private set; }

        public override bool Equals([NotNullWhen(true)] object obj)
        {
            if (obj == null) { return false; }
            if (obj.GetType() != GetType()) { return false; }

            var period = (DateTimePeriod)obj;
            return period.From == From && period.To == To;
        }

        public override string ToString()
        {
            return $"{From:dd/MM/yyyy HH:mm:ss} --> {To:dd/MM/yyyy HH:mm:ss}";
        }

        public static bool operator ==(DateTimePeriod left, DateTimePeriod right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DateTimePeriod left, DateTimePeriod right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            return $"{From.Ticks}.{To.Ticks}".GetHashCode();
        }
    }
}