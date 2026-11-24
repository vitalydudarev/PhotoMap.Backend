namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The years and the months, in UTC, the photos or the videos are taken by: those taken in one of the years, or in one
/// of the months, are taken. All of them when neither is given.
/// </summary>
/// <param name="Years">The years, such as 2019.</param>
/// <param name="Months">The months, as the year times 100 plus the month of the year, 1 to 12, such as 201903 for
/// March 2019; see <see cref="MonthKey"/>.</param>
public record DateFilter(IReadOnlyCollection<int> Years, IReadOnlyCollection<int> Months)
{
    public static DateFilter All { get; } = new([], []);

    /// <summary>
    /// Whether they are narrowed down by when they were taken.
    /// </summary>
    public bool FiltersByDate => Years.Count > 0 || Months.Count > 0;

    /// <summary>
    /// The month of the year as <see cref="Months"/> has it, such as 201903 for March 2019.
    /// </summary>
    public static int MonthKey(int year, int month) => year * 100 + month;

    /// <summary>
    /// Whether the number is a month as <see cref="Months"/> has it: a year from 1 on times 100 plus a month, 1 to 12.
    /// </summary>
    public static bool IsMonthKey(int key) => key >= 100 && key % 100 is >= 1 and <= 12;
}
