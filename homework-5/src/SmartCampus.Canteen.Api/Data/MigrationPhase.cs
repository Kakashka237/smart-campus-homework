namespace SmartCampus.Canteen.Api.Data
{
    /// <summary>
    /// Текущая фаза миграции Price > Amount по паттерну Expand and Contract.
    /// Задаётся в конфигурации: "Migration:ReceiptAmountPhase".
    /// </summary>
    public enum MigrationPhase
    {
        /// <summary>Фазы 1–2: обе колонки существуют, сервис пишет в обе (dual write).</summary>
        Expand,

        /// <summary>Фаза 3: колонка Price удалена, сервис работает только с Amount.</summary>
        Contract
    }

    /// <summary>Настройки миграции, регистрируются в DI как singleton.</summary>
    public sealed record MigrationSettings(MigrationPhase ReceiptAmountPhase);
}
