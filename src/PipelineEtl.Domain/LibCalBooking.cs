namespace PipelineEtl.Domain;

public sealed record LibCalCategory(
    string Id, string Name, string LocationId, string LocationName);

public sealed record LibCalBooking(
    string? RowId, string BookingId, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string? UserEmail, string? UserName, string? UserLastname, string? Resource,
    string? Status, string? Confirmation, string? Phone,
    string? Participant2Email, string? Participant3Email, string? Note);

public sealed record LibCalBookingPage(
    LibCalCategory Category, int Page, IReadOnlyList<LibCalBooking> Bookings, bool IsFinished);

public sealed record LibCalField(string Name, IReadOnlyList<string> Types, int NonBlankCount);

public sealed record LibCalInspection(
    LibCalCategory Category, int SampleRowCount, bool HasMore, IReadOnlyList<LibCalField> Fields);

public sealed record LibCalExtractionProgress(
    LibCalCategory Category, int PageCount, long RowCount, bool IsFinished);
