-- =====================================================================
-- CAMPUS-104 | Фаза 2. MIGRATE
-- Перенос исторических данных из Price в Amount.
--
-- Выполняется ПОСЛЕ того, как все экземпляры сервиса обновлены до v1.9.0
-- (dual write включён). Данные переносятся пачками по 5 000 строк,
-- каждая пачка — отдельная короткая транзакция. Так не создаются долгие
-- блокировки строк и не раздувается WAL, а столовая продолжает принимать оплату.
-- Скрипт можно прервать и перезапустить: обрабатываются только строки с Amount IS NULL.
-- Запуск: psql -v ON_ERROR_STOP=1 -f 02_migrate.sql (НЕ внутри BEGIN ... COMMIT,
-- иначе COMMIT внутри блока DO недопустим).
-- =====================================================================

SET lock_timeout = '3s';

DO $$
DECLARE
    batch_size  constant integer := 5000;
    moved       integer;
    total_moved bigint := 0;
BEGIN
    LOOP
        WITH batch AS (
            SELECT "Id"
            FROM "Receipts"
            WHERE "Amount" IS NULL
              AND "Price"  IS NOT NULL
            LIMIT batch_size
            FOR UPDATE SKIP LOCKED
        )
        UPDATE "Receipts" r
        SET "Amount" = r."Price"
        FROM batch
        WHERE r."Id" = batch."Id";

        GET DIAGNOSTICS moved = ROW_COUNT;
        total_moved := total_moved + moved;
        RAISE NOTICE 'CAMPUS-104: перенесено % строк (всего %)', moved, total_moved;

        EXIT WHEN moved = 0;

        COMMIT;                     -- фиксируем каждую пачку отдельно (PostgreSQL 11+)
        PERFORM pg_sleep(0.1);      -- пауза, чтобы не мешать боевой нагрузке
    END LOOP;
END;
$$;

-- Контроль: расхождений между колонками быть не должно
SELECT count(*) AS not_migrated
FROM "Receipts"
WHERE "Amount" IS NULL
   OR "Amount" IS DISTINCT FROM "Price";

-- После успешной проверки делаем новую колонку обязательной.
-- NOT VALID + VALIDATE не блокирует запись на время проверки всей таблицы.
-- DROP ... IF EXISTS делает этот шаг повторяемым при перезапуске скрипта.
ALTER TABLE "Receipts"
    DROP CONSTRAINT IF EXISTS chk_receipts_amount_not_null;

ALTER TABLE "Receipts"
    ADD CONSTRAINT chk_receipts_amount_not_null CHECK ("Amount" IS NOT NULL) NOT VALID;

ALTER TABLE "Receipts"
    VALIDATE CONSTRAINT chk_receipts_amount_not_null;
