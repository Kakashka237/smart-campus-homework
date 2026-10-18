using SmartCampus.Canteen.Api.Models;

namespace SmartCampus.Canteen.Api.Data
{
    /// <summary>
    /// Фаза 1 (Expand): код двойной записи (dual write).
    /// Пока старые экземпляры сервиса v1.8.x и отчёты бухгалтерии читают колонку Price,
    /// новый сервис v1.9.0 записывает сумму одновременно в Price и в Amount,
    /// а читает — из Amount с откатом на Price для ещё не перенесённых строк.
    /// </summary>
    public static class ReceiptAmountWriter
    {
#pragma warning disable CS0618 // обращение к устаревшей колонке Price — намеренное
        public static void SetAmount(Receipt receipt, decimal amount, MigrationPhase phase)
        {
            receipt.Amount = amount;

            if (phase == MigrationPhase.Expand)
            {
                receipt.Price = amount; // запись в старую структуру
            }
        }

        public static decimal GetAmount(Receipt receipt, MigrationPhase phase)
        {
            if (phase == MigrationPhase.Expand)
            {
                // Исторические строки до выполнения 02_migrate.sql имеют Amount = NULL.
                return receipt.Amount ?? receipt.Price ?? 0m;
            }

            return receipt.Amount ?? 0m;
        }
#pragma warning restore CS0618
    }
}
