using Wrapsfer.Application.Billing;

namespace Wrapsfer.Application.Tests.Billing;

public class StockReportProrationCalculatorTests
{
    private const int MonthlyAmountMinor = 1000; // MYR 10.00 in sen
    private static readonly TimeZoneInfo s_singapore = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");

    private readonly StockReportProrationCalculator _calculator = new();

    // Singapore is UTC+8 year-round, so 04:00 UTC is local noon on the same calendar day.
    private static DateTime SingaporeNoonUtc(int year, int month, int day) =>
        new(year, month, day, 4, 0, 0, DateTimeKind.Utc);

    [Theory]
    // 30-day month (June): first day, the 23rd from the brief, and the last day.
    [InlineData(2026, 6, 1, 30, 30, 1000)]
    [InlineData(2026, 6, 23, 8, 30, 267)] // round(8/30*1000) = 267 => MYR 2.67
    [InlineData(2026, 6, 30, 1, 30, 33)]  // round(1/30*1000) = 33
    // 31-day month (January).
    [InlineData(2026, 1, 1, 31, 31, 1000)]
    [InlineData(2026, 1, 31, 1, 31, 32)]  // round(1/31*1000) = 32
    // 28-day month (February 2026, non-leap).
    [InlineData(2026, 2, 1, 28, 28, 1000)]
    [InlineData(2026, 2, 14, 15, 28, 536)] // round(15/28*1000) = 536
    // 29-day month (February 2028, leap year).
    [InlineData(2028, 2, 29, 1, 29, 34)]  // round(1/29*1000) = 34
    public void Calculate_ProratesInclusiveDays(
        int year, int month, int day, int expectedDaysRemaining, int expectedDaysInMonth, int expectedAmountMinor)
    {
        StockReportBillingPeriod period = _calculator.Calculate(
            SingaporeNoonUtc(year, month, day), s_singapore, MonthlyAmountMinor);

        period.DaysRemaining.Should().Be(expectedDaysRemaining);
        period.DaysInMonth.Should().Be(expectedDaysInMonth);
        period.AmountMinor.Should().Be(expectedAmountMinor);
    }

    [Fact]
    public void Calculate_June23_MatchesBriefExample()
    {
        StockReportBillingPeriod period = _calculator.Calculate(
            SingaporeNoonUtc(2026, 6, 23), s_singapore, MonthlyAmountMinor);

        // MYR 2.67 prorated for 23 June through 30 June.
        period.AmountMinor.Should().Be(267);
        period.AccessEndDate.Should().Be(new DateOnly(2026, 6, 30));

        // Window spans the whole Singapore calendar month, stored as UTC instants (+8 offset).
        period.ValidFromUtc.Should().Be(new DateTime(2026, 5, 31, 16, 0, 0, DateTimeKind.Utc));
        period.ValidToUtc.Should().Be(new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Calculate_WhenMonthlyAmountNotPositive_Throws()
    {
        Action act = () => _calculator.Calculate(SingaporeNoonUtc(2026, 6, 23), s_singapore, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
