namespace Application.Common;

public static class PlanDates
{
    // Wide enough for any real plan, narrow enough that week arithmetic cannot run off the calendar.
    public static bool IsValid(DateOnly date) => date.Year is >= 2000 and <= 2200;
}
