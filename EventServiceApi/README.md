# EventService (ASP.NET Core Web API)

Сервис событий с хранилищем в PostgreSQL (EF Core, схема управляется миграциями), CRUD REST API, валидацией и Swagger.

Решение построено по слоистой (Clean Architecture-подобной) архитектуре: предметная область, бизнес-логика, инфраструктура и presentation разнесены по отдельным проектам с однонаправленными зависимостями.

## Требования
- .NET SDK 10.0+
- PostgreSQL
- Docker — для запуска интеграционных тестов (см. раздел «Тесты»)

## Структура решения и назначение слоёв

```
EventServiceApi.slnx
├── EventService.Domain           — доменный слой
├── EventService.Application      — слой бизнес-логики (use cases)
├── EventService.Infrastructure   — слой инфраструктуры
├── EventServiceApi               — presentation-слой (Web API, composition root)
├── EventService.Tests            — unit-тесты (Application/Domain)
└── EventApi.IntegrationTests     — интеграционные тесты (Infrastructure)
```

Направление зависимостей строго одностороннее — каждый слой ссылается только на слои левее себя:

```
EventServiceApi ──▶ EventService.Application ──▶ EventService.Domain
       └────────────▶ EventService.Infrastructure ──▶ EventService.Application ──▶ EventService.Domain
```

`EventService.Domain` не зависит ни от одного другого проекта и не содержит ссылок на ASP.NET Core/EF Core. `EventService.Application` зависит только от `EventService.Domain` и ничего не знает про `EventService.Infrastructure`.

### EventService.Domain

Предметная область без привязки к технологиям (нет пакетов, нет `ProjectReference`):
- **Entities** (`Entities/Event.cs`, `Entities/Booking.cs`) — доменные сущности с бизнес-инвариантами (например, `Event.TryReserveSeats`/`ReleaseSeats`, `Booking.Confirm`/`Reject`);
- **Enums** (`Enums/BookingStatus.cs`) — доменные перечисления;
- **Exceptions** (`Exceptions/NotFoundException.cs`, `Exceptions/NoAvailableSeatsException.cs`) — исключения, отражающие нарушение бизнес-правил.

### EventService.Application

Бизнес-логика (use cases) и абстракции портов, ссылается только на `EventService.Domain`:
- **Interfaces** — интерфейсы сервисов (`IEventService`, `IBookingService`) и интерфейсы портов к инфраструктуре (`IEventRepository`, `IBookingRepository`, `IUnitOfWork`) — то, что Application ожидает от инфраструктуры;
- **Services** — реализации use case'ов (`EventService`, `BookingService`);
- **Dto** — объекты передачи данных между Presentation и Application (`EventCreateDto`, `EventUpdateDto`, `EventResponseDto`, `BookingResponseDto`, `PaginatedResult<T>`);
- **Mappings** — маппинг между доменными сущностями и DTO (`BookingMappings`);
- **DependencyInjection** — `ApplicationServiceCollectionExtensions.AddApplicationServices()` регистрирует use case-сервисы в DI-контейнере.

### EventService.Infrastructure

Реализации, зависящие от внешних технологий; ссылается на `EventService.Domain` и `EventService.Application`:
- **DataAccess** — `AppDbContext`, EF Core-конфигурации сущностей (`Configurations/*`), реализации репозиториев (`Repositories/EventRepository.cs`, `Repositories/BookingRepository.cs`), `UnitOfWork`, миграции (`Migrations/*`);
- **BackgroundServices** — `BookingProcessingBackgroundService`, адаптер к фоновой обработке броней;
- **DependencyInjection** — `InfrastructureServiceCollectionExtensions.AddInfrastructureServices(...)` регистрирует `AppDbContext`, репозитории, `IUnitOfWork` и hosted-сервис.

### EventServiceApi (Presentation)

Тонкий Web API слой; ссылается на `EventService.Application` и `EventService.Infrastructure` только для того, чтобы собрать приложение в `Program.cs`:
- **Controllers** — эндпоинты, которые парсят HTTP-запрос, вызывают сервис из Application и возвращают ответ; бизнес-логики не содержат;
- **Middleware** — `ExceptionHandlingMiddleware`, глобальный обработчик исключений, маппящий доменные исключения в HTTP-статусы (`NotFoundException` → 404, `NoAvailableSeatsException` → 409 и т. д.);
- **Program.cs** — composition root: настраивает веб-хост (контроллеры, Swagger, валидация) и регистрирует зависимости слоёв через `builder.Services.AddApplicationServices()` и `builder.Services.AddInfrastructureServices(connectionString)`, не дублируя логику регистрации.

### Тестовые проекты

- **EventService.Tests** — unit-тесты сервисов Application; поднимают DI-контейнер через `AddApplicationServices()`/`AddInfrastructureServices(...)` с EF Core InMemory provider;
- **EventApi.IntegrationTests** — интеграционные тесты репозиториев Infrastructure против реального PostgreSQL (Testcontainers). Ссылаются на `EventService.Domain` и `EventService.Infrastructure`.

## Настройка строки подключения (PostgreSQL)

Строка подключения задаётся через конфигурацию `ConnectionStrings:DefaultConnection`.

### Вариант 1: appsettings.Development.json
Добавьте/обновите:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=event_service;Username=postgres;Password=postgres"
  }
}
```

### Вариант 2: переменная окружения

Windows (PowerShell):
```
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=event_service;Username=postgres;Password=postgres"
```
Linux/macOS:
```
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=event_service;Username=postgres;Password=postgres"
```

### Схема БД управляется миграциями EF Core

Схема базы данных описывается миграциями EF Core (папка `EventService.Infrastructure/DataAccess/Migrations`), а не создаётся "на лету" через `EnsureCreated()`. При запуске приложения все ещё не применённые миграции накатываются автоматически (в `Program.cs`):
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
```

#### Инструмент dotnet-ef

Если `dotnet-ef` ещё не установлен глобально:
```bash
dotnet tool install --global dotnet-ef
```

#### Создание новой миграции

`AppDbContext`, конфигурации сущностей и сами миграции живут в `EventService.Infrastructure` (`DataAccess/`), а не в `EventServiceApi` — поэтому `dotnet ef` вызывается с двумя разными проектами:
- `--project EventService.Infrastructure` — куда положить файл миграции (там же лежит `DbContext`);
- `--startup-project EventServiceApi` — откуда брать конфигурацию (строку подключения, DI) для генерации миграции, так как сам `EventService.Infrastructure` не является исполняемым приложением.

После изменения сущностей (`Event`, `Booking` в `EventService.Domain`) или конфигураций EF Core (`IEntityTypeConfiguration<T>` в `EventService.Infrastructure/DataAccess/Configurations`) создайте миграцию из корня репозитория:
```bash
dotnet ef migrations add <ИмяМиграции> --project EventService.Infrastructure --startup-project EventServiceApi
```

#### Применение миграций к базе данных

Накатить все не применённые миграции на БД, указанную в `ConnectionStrings:DefaultConnection`:
```bash
dotnet ef database update --project EventService.Infrastructure --startup-project EventServiceApi
```

Вручную это делать не обязательно — то же самое произойдёт автоматически при старте приложения (`db.Database.Migrate()` в `Program.cs`).

## Запуск
Решение разбито на несколько проектов, поэтому запускать нужно явно проект `EventServiceApi` (Presentation-слой, содержит `Program.cs`):

```bash
dotnet restore
dotnet run --project EventServiceApi
```

## Swagger
В режиме Development доступен Swagger UI:
https://localhost:7041/swagger/index.html

## GET /events — фильтрация
Поддерживаются query-параметры фильтрации (все опциональные):

title (string) — поиск по названию (частичное совпадение, регистронезависимо)

from (DateTime) — события, которые начинаются не раньше указанной даты (StartAt >= from)

to (DateTime) — события, которые заканчиваются не позже указанной даты (EndAt <= to)

Все фильтры применяются совместно (логическое И).

Примеры:

GET /events?title=conf
GET /events?from=2026-06-01T00:00:00&to=2026-06-30T23:59:59
GET /events?title=meet&from=2026-06-01T00:00:00&to=2026-06-30T23:59:59

## GET /events — пагинация
Поддерживаются query-параметры пагинации (все опциональные):

page (int, по умолчанию 1) — номер страницы (нумерация с 1)
pageSize (int, по умолчанию 10) — количество элементов на странице
Примеры:
GET /events?page=1&pageSize=10
GET /events?page=2&pageSize=5
GET /events?title=conf&page=1&pageSize=3

## Формат успешного ответа GET /events (200 OK)
GET /events возвращает пагинированный результат:
{
  "totalCount": 12,
  "page": 2,
  "count": 5,
  "items": [
    {
      "id": "guid",
      "title": "string",
      "description": "string",
      "startAt": "2026-06-01T10:00:00Z",
      "endAt": "2026-06-01T11:00:00Z"
    }
  ]
}

## Модель Booking
Booking — бронь на участие в событии.

Поля:
Id: Guid — уникальный идентификатор брони (генерируется при создании).
EventId: Guid — идентификатор события, к которому относится бронь.
Status: BookingStatus — текущий статус брони.
CreatedAt: DateTime — дата/время создания брони (устанавливается при создании).
ProcessedAt: DateTime? — дата/время обработки брони (заполняется после обработки).

## Статусы (BookingStatus)
Pending — бронь создана и ожидает обработки.
Confirmed — бронь подтверждена (в текущей версии используется как результат фоновой обработки).
Rejected — бронь отклонена.

## Модель Event
Event — событие.

Поля:
Id: Guid - Идентификатор события.
Title: string - Заголовок события.
Description: string? - Описание события (опционально).
StartAt: DateTime - Дата и время начала события.
EndAt: DateTime - Дата и время окончания события.
TotalSeats: int - Общее количество мест на событии.
AvailableSeats: int - Текущее количество свободных мест.

### Эндпоинт бронирования
## GET /bookings/{id}
Возвращает информацию о брони по её идентификатору.

Вызывает BookingService.GetBookingByIdAsync(bookingId).

- Успех: 200 OK
- Если бронь не найдена: 404 Not Found
- Ошибки: в формате Problem Details (RFC 7807) (application/problem+json)

Пример запроса:

GET /bookings/2c2f1a10-1f3a-4c2d-9b11-8a0c1d2e3f44

Пример успешного ответа:

HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "2c2f1a10-1f3a-4c2d-9b11-8a0c1d2e3f44",
  "eventId": "6f1b2c2a-2a2f-4f7b-9a0d-3c0f4c2a1d11",
  "status": "0",
  "createdAt": "2026-06-10T18:01:23Z",
  "processedAt": null
}

Пример ответа, если бронь не найдена (404 Not Found):

HTTP/1.1 404 Not Found

## POST /events/{id}/book
Создаёт бронь для указанного события.

Вызывает BookingService.CreateBookingAsync(eventId)
Возвращает 202 Accepted
В теле ответа возвращает информацию о созданной брони: Id, EventId, Status
В заголовке Location возвращает ссылку на ресурс брони: /bookings/{bookingId}
Если событие не найдено — возвращает 404 Not Found

Пример запроса:
POST /events/6f1b2c2a-2a2f-4f7b-9a0d-3c0f4c2a1d11/book

Пример ответа:
HTTP/1.1 202 Accepted
Location: /bookings/2c2f1a10-1f3a-4c2d-9b11-8a0c1d2e3f44
Content-Type: application/json
{
  "id": "2c2f1a10-1f3a-4c2d-9b11-8a0c1d2e3f44",
  "eventId": "6f1b2c2a-2a2f-4f7b-9a0d-3c0f4c2a1d11",
  "status": "0"
}

## Формат ответа при ошибках
Ошибки возвращаются в формате Problem Details (RFC 7807) (Content-Type: application/problem+json).

Пример:
{
  "type": "about:blank",
  "title": "Bad Request",
  "status": 400,
  "detail": "Дата окончания должна быть позже даты начала.",
  "instance": "/events"
}

## HTTP статусы
400 Bad Request — ошибки валидации/некорректные параметры запроса
404 Not Found — ресурс не найден
500 Internal Server Error — непредвиденная ошибка сервера
409 Conflict - при отсутствии мест

## Тесты

В решении два тестовых проекта:

### Unit-тесты (EventService.Tests)

Тесты написаны на xUnit и используют EF Core InMemory provider: `AppDbContext` настраивается через `UseInMemoryDatabase(...)` и поднимается через DI (`ServiceCollection`) с уникальным именем базы данных на тестовый класс, чтобы тесты не влияли друг на друга.

```bash
dotnet test EventService.Tests
```

### Интеграционные тесты (EventApi.IntegrationTests)

Тесты репозиториев (`EventRepository`, `BookingRepository`) написаны на xUnit и запускаются против **реального PostgreSQL**, поднятого автоматически через [Testcontainers](https://dotnet.testcontainers.org/) — Docker-образ `postgres:16-alpine` стартует и останавливается самим тестовым прогоном, вручную поднимать контейнер (`docker compose up`) не нужно.

**Требуется установленный и запущенный Docker** (Docker Desktop на Windows/macOS или Docker Engine на Linux) — без него тесты не смогут поднять контейнер и упадут при старте.

Особенности:
- один контейнер PostgreSQL используется всеми тестами прогона (xUnit collection fixture);
- перед каждым тестом база приводится к чистому состоянию (`EnsureDeleted()` + `Migrate()`), поэтому тесты изолированы и не зависят от порядка запуска.

```bash
dotnet test EventApi.IntegrationTests
```

### Все тесты сразу

Запуск из корня репозитория (потребует Docker для интеграционных тестов):

```bash
dotnet test
```
## Фоновая обработка бронирований

В приложении запущен фоновый сервис `BookingProcessingBackgroundService` (на базе `BackgroundService`), который автоматически обрабатывает бронирования.

Как работает:

1. Сервис с заданным интервалом (poll interval) опрашивает через `IBookingService.GetPendingBookingsAsync` список броней в статусе `Pending` (данные читаются из PostgreSQL через `IBookingRepository`).

2. Обработка pending-броней запускается **параллельно** (через `Task.WhenAll`), чтобы ожидание внешней системы не блокировало обработку других броней.

3. Для каждой брони выполняется искусственная задержка `Task.Delay(2 секунды)`, имитирующая обращение к внешней системе (например, платёжный шлюз/CRM/сервис подтверждения).
   Важно: задержка выполняется **до** вызова `TryProcessPendingAsync`, поэтому ожидание происходит параллельно, а не последовательно внутри критической секции.

4. После задержки бронь переводится в статус `Confirmed` (метод `BookingService.TryProcessPendingAsync`) или `Rejected` (в сценариях отклонения/ошибки — `TryRejectPendingAsync`), заполняется поле `ProcessedAt`, изменения сохраняются в БД через `IUnitOfWork.SaveChangesAsync`.

### Синхронизация при фоновой обработке

Обновление статуса брони (`BookingService.TryProcessPendingAsync` / `TryRejectPendingAsync`) сериализуется через `SemaphoreSlim` внутри `BookingService`, чтобы несколько параллельных обработок не применяли конфликтующие изменения к состоянию одной и той же брони/события.

`SemaphoreSlim` используется вместо `lock`, потому что внутри критической секции есть `await` (обращения к репозиторию и `SaveChangesAsync`), а `lock` нельзя безопасно удерживать вокруг асинхронного кода.

#### Конкурентность и защита от повторной обработки
Для предотвращения повторной обработки одной и той же брони `TryProcessPendingAsync`/`TryRejectPendingAsync` перед изменением статуса проверяют, что бронь всё ещё находится в статусе `Pending` — если статус уже сменился, обновление не выполняется и метод возвращает `false`.

## Пример полного сценария (Swagger walkthrough)
1. Откройте Swagger UI
Перейдите в браузере на: https://localhost:7041/swagger/index.html

2. Создайте событие (3 места)
В Swagger найдите Events → POST /events → Try it out и отправьте, например:
```
{
  "title": "DotNet Meetup",
  "description": "Встреча разработчиков",
  "startAt": "2026-06-10T18:00:00Z",
  "endAt": "2026-06-10T20:00:00Z",
  "totalSeats": 3
}
```
Нажмите Execute.

Ожидаемый результат: 
- 201 Created

- в ответе вернётся объект события, включая:
  - id
  - totalSeats = 3
  - availableSeats = 3

В ответе вы получите объект события. Скопируйте id созданного события (далее eventId).

3. Создайте бронь на событие (3 брони)
В Swagger найдите Events → POST /events/{id}/book → Try it out, подставьте id созданного события и отправьте поочередно запрос 3 раза, нажав Execute.

Ожидаемый результат для каждой из трёх попыток:
- HTTP статус: 202 Accepted
- заголовок ответа Location: /bookings/{bookingId}
- в теле будет информация о брони, включая id (далее bookingId) и status = 0

Сохраните id каждой брони (минимум одной) для проверки статуса далее
 
4. Попытаться создать 4-ю бронь (должен быть отказ)

Выполните четвёртый запрос:

POST /events/{id}/book

Ожидаемый результат:

- 409 Conflict
- тело ответа в формате application/problem+json (ProblemDetails)
- detail содержит сообщение:
  - "No available seats for this event"

Это подтверждает, что овербукинг предотвращён: мест было 3, четвёртая бронь не создаётся.

5. Дождаться фоновой обработки и проверить статус брони

Подождите ~3–6 секунд, затем выполните:

GET /bookings/{id}

Где {id} — идентификатор одной из броней, созданных на шаге 2.

Ожидаемый результат:

- 200 OK
- у брони:
  - status = Confirmed (или 1, если enum сериализуется числом)
  - processedAt заполнено (не null)

Пример ответа:
```
{
  "id": "....",
  "eventId": "....",
  "status": 1,
  "createdAt": "2026-06-19T18:35:46.3434373Z",
  "processedAt": "2026-06-19T18:35:50.581265Z"
}
```

## Синхронизация и конкурентность

В проекте используются несколько примитивов синхронизации, чтобы корректно работать при параллельных запросах и фоновой обработке.

### `lock` в `Event.TryReserveSeats()` и `Event.ReleaseSeats()`

**Где:** `Event`, поле:

```csharp
private readonly object _seatsLock = new();
```
Зачем нужен: делает операции изменения `AvailableSeats` потокобезопасными на уровне конкретного экземпляра `Event`:

- `TryReserveSeats` проверяет и уменьшает `AvailableSeats` атомарно;
- `ReleaseSeats` увеличивает `AvailableSeats` атомарно и не даёт превысить `TotalSeats`.

`lock` тут уместен, потому что внутри секции нет `await` — код синхронный и быстрый.

### `SemaphoreSlim`

**Где:** `BookingService`, поле:

```csharp
private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);
```

Сериализует критические секции `CreateBookingAsync`, `TryProcessPendingAsync` и `TryRejectPendingAsync`, в которых состояние брони и события читается/изменяется через репозитории с последующим `IUnitOfWork.SaveChangesAsync` — то есть асинхронный код с `await`.

Почему `SemaphoreSlim`, а не `lock`: `lock` нельзя безопасно удерживать вокруг кода с `await` (может привести к разрыву выполнения и дедлокам). `SemaphoreSlim` — асинхронный аналог мьютекса: позволяет `await WaitAsync()` и гарантированно освобождать ресурс в `finally`.

В `BookingProcessingBackgroundService` задержка `Task.Delay` (имитация внешнего вызова) выполняется **до** вызова `TryProcessPendingAsync`, то есть до захвата семафора — ожидание для разных броней идёт параллельно, а семафор удерживается только на время самого обновления состояния.

### Защита от повторной обработки одной и той же брони

`TryProcessPendingAsync` и `TryRejectPendingAsync` внутри критической секции (под семафором) проверяют текущий статус брони и меняют его только если она всё ещё `Pending`:

```csharp
var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);
if (booking is null) return false;
if (booking.Status != BookingStatus.Pending) return false;
booking.Confirm(); // или booking.Reject()
await _unitOfWork.SaveChangesAsync(cancellationToken);
```

Комбинация «семафор + проверка статуса перед изменением» гарантирует: даже если несколько задач одновременно попытаются обработать одну и ту же бронь, повторно применить `Confirm`/`Reject` к уже обработанной брони не получится.