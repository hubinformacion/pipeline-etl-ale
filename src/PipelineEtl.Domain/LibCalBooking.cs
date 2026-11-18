namespace PipelineEtl.Domain;

public sealed record LibCalCategory(
    string Id, string Name, string LocationId, string LocationName, string SourceName, string FormId);

public sealed record LibCalBooking(
    string RowId, string BookingId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string PayloadJson);

public sealed record LibCalBookingPage(
    LibCalCategory Category, int Page, IReadOnlyList<LibCalBooking> Bookings, bool IsFinished);

public sealed record LibCalField(string Name, IReadOnlyList<string> Types);

public sealed record LibCalInspection(
    LibCalCategory Category, int SampleRowCount, bool HasMore, IReadOnlyList<LibCalField> Fields);

public sealed record LibCalExtractionProgress(
    LibCalCategory Category, int PageCount, long RowCount, bool IsFinished);
