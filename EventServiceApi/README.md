# EventServiceApi — микросервисы бронирования событий

Монолит разделён на три независимых сервиса, каждый со своей базой данных, плюс общая
библиотека контрактов для асинхронного обмена сообщениями через Kafka.

## Архитектура

```
Contracts/            — общий контракт события BookingConfirmed + имя топика Kafka
Users.*/              — регистрация, вход, хеширование пароля, выдача JWT
Events.*/             — CRUD событий, учёт доступных мест (подписчик Kafka)
Bookings.*/           — создание/отмена брони (издатель Kafka)
```

Каждый сервис построен по чистой архитектуре: `*.Domain` → `*.Application` → `*.Infrastructure` → `*.Api`.
У каждого сервиса — собственная база Postgres и собственный `DbContext`; сущности не имеют
навигационных свойств на сущности других сервисов — только идентификаторы (`UserId`, `EventId`).

| Сервис   | Порт (HTTPS / HTTP) | База данных (Postgres)      | Выдаёт JWT | Проверяет JWT |
|----------|----------------------|------------------------------|:----------:|:-------------:|
| Users    | 7041 / 5041           | `localhost:5433/users`       | ✅         | ✅             |
| Events   | 7042 / 5042           | `localhost:5434/events`      | ❌         | ✅             |
| Bookings | 7043 / 5043           | `localhost:5435/bookings`    | ❌         | ✅             |

### Взаимодействие сервисов

Bookings и Events **не вызывают друг друга напрямую** — единственная связь между ними
асинхронная, через Kafka:

1. Клиент создаёт бронь: `POST /bookings` (Bookings) → бронь сохраняется в статусе `Pending`
   в базе Bookings (единственная синхронная проверка — лимит активных броней пользователя).
   **`EventId` не проверяется на существование** — Bookings не обращается к Events синхронно,
   поэтому бронь с несуществующим `EventId` будет создана и переведена в `Confirmed` как обычно.
2. Фоновый обработчик (`BookingProcessingBackgroundService`) подтверждает «висящие» брони:
   сохраняет статус `Confirmed` в своей базе, и **только после этого** публикует событие
   `BookingConfirmed` в топик Kafka `booking-confirmed` (ключ сообщения — `EventId`, чтобы все
   брони по одному событию обрабатывались последовательно в одном разделе).
3. Events подписан на этот топик (`BookingConfirmedConsumerBackgroundService`, группа
   `events-service-group`) и по каждому сообщению уменьшает `AvailableSeats` соответствующего
   события. **Именно здесь, а не в Bookings, впервые проверяется существование `EventId`**: если
   событие не найдено или мест не осталось — сообщение логируется с предупреждением и
   пропускается, подписчик не падает. Это единственное место, где несуществующий `EventId`
   вообще обнаруживается — обратной связи в Bookings нет: бронь так и останется `Confirmed`,
   никакой ошибки клиенту не возвращается (она была бы возвращена уже до подтверждения, поэтому
   узнать о невалидном `EventId` до создания брони нельзя).
4. Топик `booking-confirmed` создаётся при старте Events отдельным hosted-сервисом
   (`KafkaTopicInitializerHostedService`, через Kafka admin client) — если топик уже существует,
   это не ошибка; если создать не удалось — старт сервиса всё равно продолжается.

Это осознанная eventual-consistency модель: Bookings ничего не знает про свободные места,
Events — про жизненный цикл брони. Как следствие, `POST /bookings` **намеренно не валидирует
`EventId` синхронно** — цена этого выбора в том, что бронь на несуществующее событие успешно
создаётся и подтверждается на стороне Bookings, а единственный сигнал о проблеме — предупреждение
в логах Events (`BookingConfirmedEvent references unknown EventId {EventId}. Skipping.`).

### Общий JWT

Токен выдаёт только **Users** (`POST /auth/login`, `POST /auth/register`). Events и Bookings
только проверяют подпись — во всех трёх `appsettings.json` секции `Jwt:Secret`/`Issuer`/`Audience`
должны быть идентичны, иначе токен, выданный Users, не пройдёт проверку в других сервисах.
Ролевые ограничения сохранены: управление событиями (`POST`/`PUT`/`DELETE /events`) — только
роль `Admin`; эндпоинты броней требуют аутентификации, идентификатор пользователя читается из
claims токена.

## Инфраструктура (Docker Compose)

`docker-compose.yml` поднимает только инфраструктуру — Kafka, Zookeeper и три базы Postgres.
Сами сервисы (`Users.Api`, `Events.Api`, `Bookings.Api`) запускаются локально через `dotnet run`.

```bash
docker compose up -d
```

Поднимаются:
- `zookeeper`, `kafka` — брокер доступен с хоста на `localhost:9092`;
- `users-db` (`localhost:5433`), `events-db` (`localhost:5434`), `bookings-db` (`localhost:5435`)
  — Postgres, порты проброшены на хост, чтобы сервисы, запущенные через `dotnet run`, могли
  подключиться напрямую.

## Миграции EF Core

Миграции у каждого сервиса свои, применяются автоматически при старте (`Database.Migrate()`
в `Program.cs`). Чтобы добавить новую миграцию:

```bash
dotnet ef migrations add <Name> --project Users.Infrastructure    --startup-project Users.Api
dotnet ef migrations add <Name> --project Events.Infrastructure   --startup-project Events.Api
dotnet ef migrations add <Name> --project Bookings.Infrastructure --startup-project Bookings.Api
```

(Требуется локальный инструмент `dotnet-ef` — устанавливается через `dotnet tool restore`,
манифест лежит в `dotnet-tools.json`.)

## Запуск сервисов

```bash
docker compose up -d                       # инфраструктура: Kafka + Zookeeper + 3×Postgres
dotnet run --project Users.Api              # https://localhost:7041
dotnet run --project Events.Api             # https://localhost:7042
dotnet run --project Bookings.Api           # https://localhost:7043
```

Swagger UI доступен в Development-окружении на `/swagger` каждого сервиса.

## Основные эндпоинты

**Users** (`Users.Api`)
- `POST /auth/register` — регистрация.
- `POST /auth/login` — вход, возвращает JWT.
- `GET /users/{id}` — данные пользователя (`[Authorize]`).

**Events** (`Events.Api`)
- `GET /events`, `GET /events/{id}` — список/карточка события (публично).
- `POST /events`, `PUT /events/{id}`, `DELETE /events/{id}` — только роль `Admin`.

**Bookings** (`Bookings.Api`)
- `POST /bookings` — создать бронь (`[Authorize]`, тело `{ "eventId": "..." }`, `EventId` теперь
  передаётся в теле запроса, а не в маршруте — раньше это был `POST /events/{id}/book`).
  `EventId` не проверяется на существование в момент запроса (см. «Взаимодействие сервисов») —
  отклонение несуществующего `EventId` произойдёт асинхронно на стороне Events.
- `GET /bookings/{id}` — просмотр брони (владелец или `Admin`).
- `POST /bookings/{id}/cancel` — отмена брони (`[Authorize]`).
- `DELETE /bookings/{id}` — только роль `Admin`.

## Тесты

На каждый сервис — по два тестовых проекта:

```bash
dotnet test Users.Tests               # unit-тесты (Moq + FluentAssertions)
dotnet test Users.IntegrationTests    # интеграционные тесты (Testcontainers.PostgreSql)
dotnet test Events.Tests
dotnet test Events.IntegrationTests
dotnet test Bookings.Tests
dotnet test Bookings.IntegrationTests
```

Интеграционные тесты поднимают Postgres в контейнере через Testcontainers — для их запуска
нужен установленный и запущенный Docker.

## Структура решения

Полный список проектов — в `EventServiceApi.slnx`:

```
Contracts/

Users.Domain/  Users.Application/  Users.Infrastructure/  Users.Api/
Users.Tests/   Users.IntegrationTests/

Events.Domain/ Events.Application/ Events.Infrastructure/ Events.Api/
Events.Tests/  Events.IntegrationTests/

Bookings.Domain/ Bookings.Application/ Bookings.Infrastructure/ Bookings.Api/
Bookings.Tests/  Bookings.IntegrationTests/
```
