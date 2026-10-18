# Branch Protection Rules для ветки `main`

Репозиторий: `smart-campus-homework`. Настройка: **Settings > Branches > Add branch ruleset**
(GitHub) или **Settings > Repository > Protected branches** (GitLab).

## Конфигурация GitHub (ruleset для `main`)

| Правило | Значение | Зачем |
|---|---|---|
| Restrict deletions | Включено | Ветку `main` нельзя удалить |
| Block force pushes | Включено | Нельзя переписать историю релизов (`git push --force`) |
| Require a pull request before merging | Включено | Изменения попадают в `main` только через Pull Request |
| Required approvals | **2** | Одно одобрение от разработчика, второе — от владельца кода (для миграций — DBA) |
| Dismiss stale approvals when new commits are pushed | Включено | После новых коммитов ревью проводится заново |
| Require review from Code Owners | Включено | Файлы `migrations/**` проверяет DBA (см. `CODEOWNERS`) |
| Require approval of the most recent reviewable push | Включено | Автор не может одобрить свои последние изменения |
| Require conversation resolution before merging | Включено | Все замечания ревью закрыты |
| Require status checks to pass | Включено | Обязательные проверки CI/CD (см. ниже) |
| Require branches to be up to date before merging | Включено | PR проверяется на актуальной версии `main` |
| Require linear history | Включено | Только squash/rebase merge, история читается по задачам |
| Require signed commits | Включено | Подтверждение авторства для цифрового следа |
| Do not allow bypassing the above settings | Включено | Правила действуют и для администраторов |

## Обязательные проверки CI/CD (required status checks)

Описаны в `.github/workflows/ci.yml`:

| Проверка | Что проверяет |
|---|---|
| `build` | `dotnet build -warnaserror` — проект собирается без ошибок и предупреждений |
| `test` | `dotnet test` — unit- и sanity-тесты проходят |
| `commit-message` | Все коммиты в Pull Request начинаются с ключа задачи `[CAMPUS-XXX]` |

## Шаблон сообщения коммита

```
[CAMPUS-XXX] Краткое описание изменения
```

Пример: `[CAMPUS-104] Add tracing tags and business logic`.

## Файл CODEOWNERS

```
# Любые изменения — команда разработки модуля «Столовая»
*                      @smart-campus/canteen-devs

# Миграции БД дополнительно проверяет DBA
/homework-5/migrations/  @smart-campus/dba

# Эксплуатационная документация — инженер сопровождения
/homework-5/docs/        @smart-campus/ops
```

## Аналог для GitLab

- **Allowed to merge:** Maintainers; **Allowed to push and merge:** No one.
- **Allowed to force push:** выключено.
- **Merge request approvals:** 2 approvals required, *Code Owner approval* включено.
- **Pipelines must succeed** и **All threads must be resolved** включены
  (*Settings > Merge requests > Merge checks*).
- **Push rules:** `Require expression in commit messages` = `^\[CAMPUS-\d+\] .+`.
