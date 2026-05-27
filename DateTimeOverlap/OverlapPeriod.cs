namespace CustomsCloud.InfrastructureCore.Common.Date
{
    public struct OverlapPeriod
    {
        public OverlapPeriod(DateTimePeriod period1, DateTimePeriod period2)
        {
            Period1 = period1;
            Period2 = period2;
        }

        public DateTimePeriod Period1 { get; private set; }
        public DateTimePeriod Period2 { get; private set; }
    }
}