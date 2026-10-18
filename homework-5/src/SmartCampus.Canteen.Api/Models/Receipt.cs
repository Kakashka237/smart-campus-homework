namespace SmartCampus.Canteen.Api.Models
{
    /// <summary>
    /// Чек безналичной оплаты в столовой ИС «Умный кампус».
    /// Таблица "Receipts" в PostgreSQL.
    /// </summary>
    public class Receipt
    {
        public Guid Id { get; set; }

        /// <summary>Номер студенческого билета (электронной карты кампуса).</summary>
        public string StudentCardId { get; set; } = string.Empty;

        /// <summary>Состав заказа (например, «Комплексный обед №2»).</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// СТАРАЯ колонка суммы чека. Сохраняется только на время фаз Expand / Migrate
        /// для обратной совместимости со старыми версиями сервиса (v1.8.x) и отчётами.
        /// Будет удалена скриптом 03_contract.sql (CAMPUS-104).
        /// </summary>
        [Obsolete("Колонка Price заменена на Amount (CAMPUS-104). Используйте Amount.")]
        public decimal? Price { get; set; }

        /// <summary>НОВАЯ колонка суммы чека (добавлена скриптом 01_expand.sql).</summary>
        public decimal? Amount { get; set; }

        public string Currency { get; set; } = "RUB";

        public ReceiptStatus Status { get; set; } = ReceiptStatus.Paid;

        /// <summary>Признак технического (sanity) платежа, создаваемого после релиза.</summary>
        public bool IsSanityCheck { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum ReceiptStatus
    {
        Paid = 0,
        Refunded = 1
    }
}
