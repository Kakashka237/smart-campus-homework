# Практическая работа №5

**Проектирование безопасных релизных процессов, паттернов миграции данных и эксплуатационной документации ИС «Умный кампус»**

| | |
|---|---|
| Дисциплина | МДК 03.01, ПМ 03 «Сопровождение и обслуживание программного обеспечения компьютерных систем» |
| Студент | Мельник Иван Игоревич |
| Группа | И14-23-9 |
| Вариант | **№ 4** — «Столовая и безналичная оплата» |
| Задача Jira | CAMPUS-104 |
| Суть миграции | Переименование колонки `Price` > `Amount` в чеке |
| Релиз | v1.9.0 |

---

## Состав работы и ссылки на файлы

| Блок | Файлы |
|---|---|
| 1. Цифровой след (OpenTelemetry) | [ReceiptsController.cs](src/SmartCampus.Canteen.Api/Controllers/ReceiptsController.cs), [CampusTelemetry.cs](src/SmartCampus.Canteen.Api/Telemetry/CampusTelemetry.cs), [Program.cs](src/SmartCampus.Canteen.Api/Program.cs) |
| 2. Change Management | [CR-CAMPUS-104.md](docs/change-request/CR-CAMPUS-104.md), [branch-protection.md](docs/branch-protection.md), [ci.yml](../.github/workflows/ci.yml) |
| 3. Expand and Contract | [01_expand.sql](migrations/01_expand.sql), [02_migrate.sql](migrations/02_migrate.sql), [03_contract.sql](migrations/03_contract.sql), код двойной записи [ReceiptAmountWriter.cs](src/SmartCampus.Canteen.Api/Data/ReceiptAmountWriter.cs), [CanteenDbContext.cs](src/SmartCampus.Canteen.Api/Data/CanteenDbContext.cs) |
| 4. Sanity-тест и Docs-as-Code | [ProductionSanityTests.cs](tests/SmartCampus.Canteen.SanityTests/ProductionSanityTests.cs), [ADR-0009](docs/adr/0009-receipt-price-to-amount.md), [CHANGELOG.md](CHANGELOG.md) |

Стек: ASP.NET Core Web API (.NET 10), EF Core + Npgsql (PostgreSQL), OpenTelemetry, xUnit.

### Структура репозитория

```
homework-5/
├── src/SmartCampus.Canteen.Api/      # сервис оплаты в столовой
│   ├── Controllers/ReceiptsController.cs
│   ├── Data/                         # DbContext, фаза миграции, dual write
│   ├── Models/
│   └── Telemetry/CampusTelemetry.cs
├── tests/SmartCampus.Canteen.SanityTests/
├── migrations/                       # 00_baseline, 01_expand, 02_migrate, 03_contract
├── docs/
│   ├── adr/0009-receipt-price-to-amount.md
│   ├── change-request/CR-CAMPUS-104.md
│   └── branch-protection.md
├── CHANGELOG.md
└── REPORT.md
```

---

## Блок 1. Единый цифровой след

В контроллере `ReceiptsController` теги ставятся на `Activity.Current` — span входящего HTTP-запроса,
который создаёт инструментирование ASP.NET Core. Затем открывается дочерний бизнес-span
`Canteen.CreateReceipt`: он получает тот же `TraceId`, что и `Activity.Current`.

| Тег | Значение |
|---|---|
| `jira.issue` | `CAMPUS-104` (также передаётся в нижестоящие сервисы через W3C Baggage) |
| `campus.transaction_id` | идентификатор чека (`Receipt.Id`) — доменный идентификатор сущности |
| `campus.student_card_id`, `campus.amount`, `campus.sanity_check`, `campus.migration_phase` | дополнительные бизнес-атрибуты |

Коммит: `[CAMPUS-104] Add tracing tags and business logic`.

Фрагмент вывода OpenTelemetry Console Exporter после запроса `POST /api/v1/receipts`:

```
Activity.TraceId:            d644135ce6f6335ae9cb7a9afb04d63d
Activity.DisplayName:        Canteen.CreateReceipt
Activity.Tags:
    jira.issue: CAMPUS-104
    campus.transaction_id: 61724a3a-b7e9-4ffc-b8ed-5b6d858dfd78
    campus.student_card_id: ST-001024
    campus.amount: 245.50
    campus.sanity_check: false
    campus.migration_phase: Expand
Activity.TraceId:            d644135ce6f6335ae9cb7a9afb04d63d
Activity.DisplayName:        POST api/v1/receipts
Activity.Tags:
    jira.issue: CAMPUS-104
    campus.transaction_id: 61724a3a-b7e9-4ffc-b8ed-5b6d858dfd78
    http.route: api/v1/receipts
```

---

## Блок 2. Change Request (краткое описание)

Полная карточка: [docs/change-request/CR-CAMPUS-104.md](docs/change-request/CR-CAMPUS-104.md).

**CR-CAMPUS-104, релиз v1.9.0.** Нормальное изменение, средний риск. Колонка суммы чека `Receipts.Price`
переименовывается в `Amount`, чтобы название совпадало с остальным платёжным контуром
(эквайринг, терминалы, 1С). Переименование выполняется по паттерну Expand and Contract без остановки оплаты.
В релиз также входят теги трассировки `jira.issue` и `campus.transaction_id`, которые ускоряют разбор инцидентов.

**Окно обслуживания:** вторник 20.10.2026, **02:00–04:00 МСК**. По мониторингу это минимум трафика
(меньше 1 запроса в минуту): столовая закрыта с 20:00 до 08:00. Окно находится между ночной сверкой с банком (01:00)
и выгрузкой в 1С (05:00). Фаза 2 — 21.10.2026, фаза 3 — отдельный CR не ранее 28.10.2026.

**План работ:** резервная копия таблицы `pg_dump` > `01_expand.sql` > выкат v1.9.0 через
`kubectl set image` (RollingUpdate, `maxUnavailable: 0`) > Production Sanity-тест > проверка трасс
по `jira.issue=CAMPUS-104` > наблюдение в течение часа (5xx < 0,5 %, p95 < 300 мс).

**План отката:** при провале sanity-теста, доле 5xx больше 2 % или найденных расхождениях
`Price`/`Amount` выполняется `kubectl rollout undo deployment/canteen-api -n campus`.
Схему БД откатывать не нужно: v1.8.3 продолжает работать с `Price`, триггер поддерживает согласованность данных.

**Branch Protection для `main`** ([docs/branch-protection.md](docs/branch-protection.md)): только Pull Request,
**2 одобрения** (одно от Code Owner, для `migrations/**` — DBA), сброс одобрений после новых коммитов,
обязательные проверки CI `build` (`-warnaserror`), `test`, `commit-message` (префикс `[CAMPUS-XXX]`),
актуальность ветки, линейная история, подписанные коммиты, запрет force push и удаления ветки,
правила действуют и для администраторов.

---

## Блок 3. Expand and Contract

| Фаза | Скрипт | Что делает |
|---|---|---|
| 1. Expand | `01_expand.sql` | `ADD COLUMN "Amount" numeric(10,2) NULL` (мгновенно, без перезаписи таблицы), временный триггер синхронизации для экземпляров v1.8.x, `Price DROP NOT NULL`, `lock_timeout = 3s` |
| 1. Код | `ReceiptAmountWriter.cs` | двойная запись: `SetAmount` пишет в `Price` и `Amount`, `GetAmount` читает `Amount ?? Price` |
| 2. Migrate | `02_migrate.sql` | перенос пачками по 5 000 строк в коротких транзакциях (`FOR UPDATE SKIP LOCKED`, `COMMIT` после каждой пачки), проверка расхождений, `CHECK ... NOT VALID` + `VALIDATE` |
| 3. Contract | `03_contract.sql` | удаление триггера, `Amount SET NOT NULL`, `DROP COLUMN "Price"`; сервис переключается настройкой `Migration:ReceiptAmountPhase = Contract` |

---

## Блок 4. Sanity-тест и документация

`ProductionSanityTests.Payment_OneRuble_IsStoredInAmount_AndCleanedUp`:
1. создаёт технический платёж на **1 ₽** (`IsSanityCheck = true`);
2. проверяет, что чек читается и сумма приходит из `Amount`;
3. в блоке `finally` **всегда** выполняет Cleanup: `POST /refund` > `DELETE`;
4. проверяет, что технический чек удалён (`404 Not Found`).

Против прода тест запускается с переменной `CAMPUS_SANITY_BASE_URL`, без неё — на сервисе в памяти (`WebApplicationFactory`).

```
> dotnet test
Пройден!   : не пройдено     0, пройдено     2, пропущено     0, всего     2, длительность 2 s.
```

- **ADR:** [docs/adr/0009-receipt-price-to-amount.md](docs/adr/0009-receipt-price-to-amount.md) — сравнение четырёх вариантов
  (RENAME COLUMN, маппинг в EF, VIEW, Expand and Contract) и обоснование выбора.
- **Changelog:** [CHANGELOG.md](CHANGELOG.md) — версия 1.9.0 (Added, Fixed, Deprecated) и Breaking Changes для фазы Contract.

---

## Как запустить

```bash
dotnet test homework-5/SmartCampus.Canteen.sln
dotnet run --project homework-5/src/SmartCampus.Canteen.Api
```

Без строки подключения `ConnectionStrings:Campus` сервис использует InMemory-базу. С PostgreSQL:
сначала выполнить `migrations/00_baseline.sql`, затем фазы 01–03 по порядку.
