-- =====================================================================
-- CAMPUS-104 | Фаза 1. EXPAND
-- Переименование колонки Price > Amount в чеке без простоя приложения.
--
-- Добавляем НОВУЮ колонку рядом со старой. Старая колонка Price остаётся,
-- поэтому работающие экземпляры сервиса v1.8.x продолжают работать.
-- Новый сервис v1.9.0 пишет сумму в обе колонки (см. ReceiptAmountWriter.cs).
--
-- Свойства безопасности:
--   * ADD COLUMN без DEFAULT и NOT NULL — мгновенная операция (только метаданные),
--     таблица не переписывается, длительной блокировки нет;
--   * lock_timeout не даёт миграции «повесить» прод в очереди за блокировкой;
--   * скрипт идемпотентен (IF NOT EXISTS) — его можно безопасно перезапустить.
-- Откат: ALTER TABLE "Receipts" DROP COLUMN IF EXISTS "Amount";
-- =====================================================================

SET lock_timeout = '3s';
SET statement_timeout = '30s';

BEGIN;

ALTER TABLE "Receipts"
    ADD COLUMN IF NOT EXISTS "Amount" numeric(10,2) NULL;

-- Старые экземпляры v1.8.x ничего не знают про Amount. Чтобы новые строки,
-- созданные ими во время раската, тоже получили Amount, ставим временный триггер
-- синхронизации. Он удаляется в фазе Contract.
CREATE OR REPLACE FUNCTION campus_receipts_sync_amount()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        -- v1.8.x заполняет только Price, v1.9.0 — обе колонки
        NEW."Amount" := COALESCE(NEW."Amount", NEW."Price");
        NEW."Price"  := COALESCE(NEW."Price",  NEW."Amount");
    ELSIF NEW."Price" IS DISTINCT FROM OLD."Price"
      AND NEW."Amount" IS NOT DISTINCT FROM OLD."Amount" THEN
        -- старый сервис скорректировал сумму — переносим в новую колонку
        NEW."Amount" := NEW."Price";
    ELSIF NEW."Amount" IS DISTINCT FROM OLD."Amount"
      AND NEW."Price" IS NOT DISTINCT FROM OLD."Price" THEN
        NEW."Price" := NEW."Amount";
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_receipts_sync_amount ON "Receipts";
CREATE TRIGGER trg_receipts_sync_amount
    BEFORE INSERT OR UPDATE ON "Receipts"
    FOR EACH ROW
    EXECUTE FUNCTION campus_receipts_sync_amount();

-- Старая колонка становится необязательной: сервис после Contract
-- перестанет её заполнять.
ALTER TABLE "Receipts"
    ALTER COLUMN "Price" DROP NOT NULL;

COMMENT ON COLUMN "Receipts"."Price"  IS 'DEPRECATED (CAMPUS-104): заменена на Amount, будет удалена в 03_contract.sql';
COMMENT ON COLUMN "Receipts"."Amount" IS 'Сумма чека (CAMPUS-104)';

COMMIT;
