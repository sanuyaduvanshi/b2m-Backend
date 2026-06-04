using Pettle.Domain.DailyTasks;

namespace Pettle.Application.DailyTasks;

public record DailyTaskRow(
    Guid BookingServiceId,
    Guid BookingId,
    string ParentName,
    string Phone,
    string PetName,
    string ServiceType,
    string? KennelLabel,
    string? BoardingType,
    IReadOnlyDictionary<string, DailyTaskCell> Cells,
    string? CompanionName
);

public record DailyTaskCell(
    Guid EntryId,
    DailyTaskStatus Status,
    DateTimeOffset? CompletedAt,
    string? CompletedByName,
    string? Notes,
    string? Label
);

public record DailyTaskBoard(DateOnly Date, IReadOnlyList<string> Columns, IReadOnlyList<DailyTaskRow> Rows);

public record UpdateDailyTaskStatusRequest(DailyTaskStatus Status, string? Notes);

public interface IDailyTaskService
{
    Task<DailyTaskBoard> BoardAsync(DateOnly date, string? serviceType, string? status, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(Guid entryId, UpdateDailyTaskStatusRequest req, CancellationToken ct = default);
    Task<string> ExportCsvAsync(DateOnly date, string? serviceType, CancellationToken ct = default);
}

public static class DailyTaskColumns
{
    /// <summary>The default columns rendered on the board. Order matches the FRD layout.</summary>
    public static readonly IReadOnlyList<DailyTaskType> DefaultColumns = new[]
    {
        DailyTaskType.Meal1, DailyTaskType.Meal2, DailyTaskType.Meal3, DailyTaskType.Meal4,
        DailyTaskType.Medication, DailyTaskType.Walk1, DailyTaskType.Walk2,
        DailyTaskType.GroomingStep, DailyTaskType.VetRound, DailyTaskType.Note,
    };

    public static IEnumerable<DailyTaskType> ForBoarding() => DefaultColumns;
    public static IEnumerable<DailyTaskType> ForGrooming() => new[] { DailyTaskType.GroomingStep, DailyTaskType.Note };
    public static IEnumerable<DailyTaskType> ForVet() => new[] { DailyTaskType.Medication, DailyTaskType.VetRound, DailyTaskType.Note };
    public static IEnumerable<DailyTaskType> ForDayCare() => new[]
    {
        DailyTaskType.Meal1, DailyTaskType.Meal2, DailyTaskType.Walk1, DailyTaskType.Walk2, DailyTaskType.Note
    };
}
