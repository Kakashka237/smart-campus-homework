-- =====================================================================
-- CAMPUS-104 | Фаза 3. CONTRACT
-- Удаление старой структуры после адаптации всех клиентов.
--
-- Предусловия (проверяются по чек-листу в CR-CAMPUS-104):
--   1. Все экземпляры сервиса работают на v1.9.0+ с Migration:ReceiptAmountPhase = Contract
--      (сервис больше не читает и не пишет Price).
--   2. Отчёты бухгалтерии и выгрузка в 1С переведены на колонку Amount.
--   3. 02_migrate.sql выполнен, проверка not_migrated вернула 0.
--   4. Прошло не менее 7 дней стабильной работы после фазы 2.
--   5. Снята резервная копия таблицы (pg_dump -t '"Receipts"').
--
-- ВНИМАНИЕ: фаза необратима без восстановления из резервной копии.
-- =====================================================================

SET lock_timeout = '3s';
SET statement_timeout = '30s';

BEGIN;

-- 1. Удаляем временный триггер синхронизации
DROP TRIGGER IF EXISTS trg_receipts_sync_amount ON "Receipts";
DROP FUNCTION IF EXISTS campus_receipts_sync_amount();

-- 2. Заменяем CHECK на полноценный NOT NULL.
--    Благодаря валидному CHECK (Amount IS NOT NULL) PostgreSQL 12+ не сканирует таблицу.
ALTER TABLE "Receipts"
    ALTER COLUMN "Amount" SET NOT NULL;

ALTER TABLE "Receipts"
    DROP CONSTRAINT IF EXISTS chk_receipts_amount_not_null;

-- 3. Удаляем старую колонку (операция над метаданными, без перезаписи таблицы)
ALTER TABLE "Receipts"
    DROP COLUMN IF EXISTS "Price";

COMMIT;
