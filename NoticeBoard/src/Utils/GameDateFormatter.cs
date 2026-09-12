using System;
using Vintagestory.API.Common;

namespace NoticeBoard.Utils;

public static class GameDateFormatter
{
    public const int VanillaMonthDays = 9;
    public const int MinLifeDays = 1;
    public const int WearSteps = 32;
    public const int FallDurationMs = 1250;
    public const float FallDropStartU = 0.35f;
    public const float FallDropOut = 0.12f;
    public const float FallGravity = 2.6f;
    public const int FallMaxMs = 15000;
    public const int FallSearchBlocks = 64;

    private static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    public static string FormatImmersiveDate(float hoursPerDay, int daysPerMonth, double totalHours, bool includeTime = true)
    {
        int monthsPerYear = MonthNames.Length;
        int daysPerYear = daysPerMonth * monthsPerYear;
        int totalDays = (int)(totalHours / hoursPerDay);

        int year = totalDays / daysPerYear;
        int dayOfYear = totalDays % daysPerYear;
        int monthIndex = dayOfYear / daysPerMonth;
        int dayOfMonth = (dayOfYear % daysPerMonth) + 1;

        string monthName = MonthNames[Math.Clamp(monthIndex, 0, MonthNames.Length - 1)];

        if (!includeTime)
        {
            return $"Day {dayOfMonth} of {monthName}, Year {year}";
        }

        double hourOfDayRaw = totalHours % hoursPerDay;
        int hour = (int)hourOfDayRaw;
        int minute = (int)((hourOfDayRaw - hour) * 60);

        string amPm = hour >= 12 ? "PM" : "AM";
        int displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;

        return $"Day {dayOfMonth} of {monthName}, Year {year} at {displayHour}:{minute:D2} {amPm}";
    }

    public static int DefaultLifeDays(IGameCalendar cal)
    {
        int month = cal == null ? VanillaMonthDays : cal.DaysPerMonth;
        return month < MinLifeDays ? VanillaMonthDays : month;
    }

    public static int ClampLifeDays(int days)
    {
        if (days < MinLifeDays)
            return DefaultLifeDays(NoticeBoardModSystem.getSAPI()?.World?.Calendar ?? NoticeBoardModSystem.getCAPI()?.World?.Calendar);
        return days;
    }

    public static double LifeHours(float hoursPerDay, int lifeDays) => hoursPerDay * ClampLifeDays(lifeDays);

    public static double Age01(double postedHours, double nowHours, float hoursPerDay, int lifeDays)
    {
        double life = LifeHours(hoursPerDay, lifeDays);
        if (life <= 0) return 0;
        return Math.Clamp((nowHours - postedHours) / life, 0, 1);
    }

    public static double Wear01(double age01)
    {
        double t = Math.Clamp(age01, 0, 1);
        return t * t;
    }

    public static double WearTickHours(float hoursPerDay, int lifeDays)
    {
        double life = LifeHours(hoursPerDay, lifeDays);
        double perLife = life / WearSteps;
        double perDay = hoursPerDay / 4.0;
        return Math.Min(perLife, perDay);
    }

    public static int WearBuckets(int lifeDays)
    {
        int days = ClampLifeDays(lifeDays);
        return Math.Max(WearSteps, days * 4);
    }

#if DEBUG
    public static void SelfCheckWear()
    {
        if (Wear01(0) != 0)
            throw new InvalidOperationException("Wear01(0) must be 0.");
        if (Math.Abs(Wear01(1) - 1) > 1e-9)
            throw new InvalidOperationException("Wear01(1) must be 1.");
        if (Math.Abs(Wear01(0.5) - 0.25) > 1e-9)
            throw new InvalidOperationException("Wear01 is age squared, so half life is 0.25.");
        if (Math.Abs(WearTickHours(24, 1) - 0.75) > 1e-9)
            throw new InvalidOperationException("1-day life must tick life/32 (0.75h).");
        if (Math.Abs(WearTickHours(24, 9) - 6) > 1e-9)
            throw new InvalidOperationException("9-day life must cap at HoursPerDay/4 (6h).");
        if (Math.Abs(WearTickHours(24, 365) - 6) > 1e-9)
            throw new InvalidOperationException("365-day life must still cap at 6h, not life/32.");
        if (WearBuckets(1) != 32)
            throw new InvalidOperationException("1-day life must keep 32 looks.");
        if (WearBuckets(9) != 36)
            throw new InvalidOperationException("9-day life must be days*4 looks.");
        if (WearBuckets(365) != 1460)
            throw new InvalidOperationException("365-day life must be 365*4 looks.");
        if (DefaultLifeDays(null) != VanillaMonthDays)
            throw new InvalidOperationException("DefaultLifeDays(null) must be vanilla month length.");
    }
#endif
}
