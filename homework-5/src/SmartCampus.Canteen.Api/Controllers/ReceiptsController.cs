using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Canteen.Api.Data;
using SmartCampus.Canteen.Api.Models;
using SmartCampus.Canteen.Api.Telemetry;

namespace SmartCampus.Canteen.Api.Controllers
{
    /// <summary>
    /// Модуль «Столовая и безналичная оплата» ИС «Умный кампус».
    /// </summary>
    [ApiController]
    [Route("api/v1/receipts")]
    public class ReceiptsController : ControllerBase
    {
        private readonly CanteenDbContext _db;
        private readonly MigrationPhase _phase;
        private readonly ILogger<ReceiptsController> _logger;

        public ReceiptsController(CanteenDbContext db, MigrationSettings migration, ILogger<ReceiptsController> logger)
        {
            _db = db;
            _phase = migration.ReceiptAmountPhase;
            _logger = logger;
        }

        /// <summary>Безналичная оплата обеда: создание чека.</summary>
        [HttpPost]
        public async Task<ActionResult<ReceiptResponse>> Create(CreateReceiptRequest request, CancellationToken ct)
        {
            if (request.Amount <= 0)
            {
                return ValidationProblem("Сумма чека должна быть больше нуля.");
            }

            var receipt = new Receipt
            {
                Id = Guid.NewGuid(),
                StudentCardId = request.StudentCardId,
                Description = request.Description,
                IsSanityCheck = request.IsSanityCheck
            };
            ReceiptAmountWriter.SetAmount(receipt, request.Amount, _phase);

            // Теги на входящий HTTP-span, созданный инструментированием ASP.NET Core
            CampusTelemetry.TagReceipt(Activity.Current, receipt.Id, receipt.StudentCardId);

            // Дочерний бизнес-span: наследует TraceId из Activity.Current
            using (var activity = CampusTelemetry.Source.StartActivity("Canteen.CreateReceipt"))
            {
                CampusTelemetry.TagReceipt(activity, receipt.Id, receipt.StudentCardId);
                activity?.SetTag(CampusTelemetry.TagAmount, request.Amount);
                activity?.SetTag(CampusTelemetry.TagSanityCheck, request.IsSanityCheck);
                activity?.SetTag(CampusTelemetry.TagMigrationPhase, _phase.ToString());

                _db.Receipts.Add(receipt);
                await _db.SaveChangesAsync(ct);
            }

            _logger.LogInformation("[{Jira}] Чек {TransactionId} на сумму {Amount} создан, TraceId={TraceId}",
                CampusTelemetry.JiraIssue, receipt.Id, request.Amount, Activity.Current?.TraceId);

            return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, ToResponse(receipt));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ReceiptResponse>> GetById(Guid id, CancellationToken ct)
        {
            var receipt = await _db.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (receipt is null)
            {
                return NotFound();
            }

            CampusTelemetry.TagReceipt(Activity.Current, receipt.Id, receipt.StudentCardId);
            return ToResponse(receipt);
        }

        /// <summary>Возврат средств по чеку.</summary>
        [HttpPost("{id:guid}/refund")]
        public async Task<ActionResult<ReceiptResponse>> Refund(Guid id, CancellationToken ct)
        {
            var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (receipt is null)
            {
                return NotFound();
            }

            using var activity = CampusTelemetry.Source.StartActivity("Canteen.RefundReceipt");
            CampusTelemetry.TagReceipt(Activity.Current, receipt.Id, receipt.StudentCardId);

            receipt.Status = ReceiptStatus.Refunded;
            await _db.SaveChangesAsync(ct);

            return ToResponse(receipt);
        }

        /// <summary>
        /// Удаление технического чека после sanity-теста (Cleanup).
        /// Реальные чеки студентов удалять нельзя — только возвращать.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteSanityReceipt(Guid id, CancellationToken ct)
        {
            var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (receipt is null)
            {
                return NotFound();
            }

            if (!receipt.IsSanityCheck)
            {
                return Problem("Удалять можно только технические sanity-чеки.", statusCode: StatusCodes.Status403Forbidden);
            }

            CampusTelemetry.TagReceipt(Activity.Current, receipt.Id, receipt.StudentCardId);
            _db.Receipts.Remove(receipt);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        private ReceiptResponse ToResponse(Receipt r) => new(
            r.Id,
            r.StudentCardId,
            r.Description,
            ReceiptAmountWriter.GetAmount(r, _phase),
            r.Currency,
            r.Status.ToString(),
            r.IsSanityCheck,
            r.CreatedAtUtc);
    }
}
