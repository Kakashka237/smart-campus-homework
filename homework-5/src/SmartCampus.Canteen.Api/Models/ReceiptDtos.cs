namespace SmartCampus.Canteen.Api.Models
{
    public record CreateReceiptRequest(
        string StudentCardId,
        string Description,
        decimal Amount,
        bool IsSanityCheck = false);

    public record ReceiptResponse(
        Guid Id,
        string StudentCardId,
        string Description,
        decimal Amount,
        string Currency,
        string Status,
        bool IsSanityCheck,
        DateTime CreatedAtUtc);
}
