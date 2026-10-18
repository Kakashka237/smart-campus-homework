# Changelog

Все значимые изменения сервиса `SmartCampus.Canteen` (ИС «Умный кампус», модуль
«Столовая и безналичная оплата») фиксируются в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/),
проект придерживается [семантического версионирования](https://semver.org/lang/ru/).

## [Unreleased]

### Breaking Changes
- Удаление колонки `Receipts.Price` (фаза Contract, `migrations/03_contract.sql`).
  Выполняется отдельным CR не ранее 28.10.2026. Все SQL-отчёты и интеграции, которые
  читают `Price`, до этого нужно перевести на `Amount`. ([CAMPUS-104])

## [1.9.0] - 2026-10-20

### Added
- Колонка `Receipts.Amount` — новое название суммы чека (фаза Expand, `migrations/01_expand.sql`). ([CAMPUS-104])
- Двойная запись суммы в `Price` и `Amount` (`ReceiptAmountWriter`) и настройка
  `Migration:ReceiptAmountPhase` (`Expand` / `Contract`). ([CAMPUS-104])
- Временный триггер `trg_receipts_sync_amount` для синхронизации колонок, пока работают экземпляры v1.8.x. ([CAMPUS-104])
- Пакетный перенос исторических данных `migrations/02_migrate.sql`. ([CAMPUS-104])
- Теги OpenTelemetry `jira.issue` и `campus.transaction_id` на входящих запросах
  и бизнес-span'ах `Canteen.CreateReceipt` / `Canteen.RefundReceipt`, проброс `jira.issue` через W3C Baggage. ([CAMPUS-104])
- Эндпоинты `POST /api/v1/receipts/{id}/refund` и `DELETE /api/v1/receipts/{id}` (удаляет только технические чеки). ([CAMPUS-104])
- Production Sanity-тест: оплата на 1 ₽ с обязательным возвратом и удалением чека. ([CAMPUS-104])
- Эндпоинт `GET /health` с версией релиза и текущей фазой миграции.

### Fixed
- Тег `campus.transaction_id` не попадал в трассы, поэтому оплату студента нельзя было найти
  в Jaeger по номеру чека. ([CAMPUS-104])
- Сервис принимал чеки с нулевой и отрицательной суммой — теперь возвращается `400 Bad Request`. ([CAMPUS-104])

### Deprecated
- Колонка `Receipts.Price` и свойство `Receipt.Price` помечены устаревшими (`[Obsolete]`). ([CAMPUS-104])

## [1.8.3] - 2026-09-29

### Fixed
- Повторное списание при двойном нажатии на терминале оплаты. ([CAMPUS-097])

[Unreleased]: https://github.com/Kakashka237/smart-campus-homework/compare/v1.9.0...HEAD
[1.9.0]: https://github.com/Kakashka237/smart-campus-homework/compare/v1.8.3...v1.9.0
[1.8.3]: https://github.com/Kakashka237/smart-campus-homework/releases/tag/v1.8.3
[CAMPUS-104]: https://jira.campus.example/browse/CAMPUS-104
[CAMPUS-097]: https://jira.campus.example/browse/CAMPUS-097
