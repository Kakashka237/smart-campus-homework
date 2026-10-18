-- =====================================================================
-- ИС «Умный кампус», модуль «Столовая и безналичная оплата»
-- Исходное состояние схемы (версия сервиса v1.8.x) — ДО миграции CAMPUS-104.
-- Скрипт нужен только для воспроизведения стенда, в проде не выполняется.
-- =====================================================================

CREATE TABLE IF NOT EXISTS "Receipts" (
    "Id"            uuid          PRIMARY KEY,
    "StudentCardId" varchar(32)   NOT NULL,
    "Description"   varchar(256)  NOT NULL,
    "Price"         numeric(10,2) NOT NULL,
    "Currency"      varchar(3)    NOT NULL DEFAULT 'RUB',
    "Status"        integer       NOT NULL DEFAULT 0,
    "IsSanityCheck" boolean       NOT NULL DEFAULT false,
    "CreatedAtUtc"  timestamptz   NOT NULL DEFAULT now()
);

-- Тестовые исторические данные
INSERT INTO "Receipts" ("Id", "StudentCardId", "Description", "Price")
SELECT gen_random_uuid(),
       'ST-' || lpad((g % 2000)::text, 6, '0'),
       'Комплексный обед №' || (g % 5 + 1),
       round((150 + random() * 250)::numeric, 2)
FROM generate_series(1, 50000) AS g;
