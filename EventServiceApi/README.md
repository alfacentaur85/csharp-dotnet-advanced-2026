# EventService (ASP.NET Core Web API)

Простой каркас сервиса событий с in-memory хранилищем, CRUD REST API, валидацией и Swagger.

## Требования
- .NET SDK 8.0+
- PostgreSQL

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

### Автоматическое создание схемы БД
При запуске приложения схема БД создаётся автоматически через EnsureCreated() (в Program.cs):
```
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
```

### Тесты (EF Core InMemory)

В тестах используется EF Core InMemory provider: AppDbContext настраивается через UseInMemoryDatabase(...) и поднимается через DI (ServiceCollection) с уникальным именем базы данных на тестовый класс, чтобы тесты не влияли друг на друга.

## Запуск
Из корня проекта:

```bash
dotnet restore
dotnet run
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
Тесты написаны на xUnit в отдельном проекте (например, EventService.Tests).

Запуск тестов из корня решения/репозитория:

```bash
dotnet test
```
## Фоновая обработка бронирований

В приложении запущен фоновый сервис `BookingProcessingBackgroundService` (на базе `BackgroundService`), который автоматически обрабатывает бронирования.

Как работает:

1. Сервис с заданным интервалом (poll interval) опрашивает хранилище бронирований и получает список броней в статусе `Pending`.

2. Обработка pending-броней запускается **параллельно** (через `Task.WhenAll`), чтобы ожидание внешней системы не блокировало обработку других броней.

3. Для каждой брони выполняется искусственная задержка `Task.Delay(2 секунды)`, имитирующая обращение к внешней системе (например, платёжный шлюз/CRM/сервис подтверждения).  
   Важно: задержка выполняется **до входа в критическую секцию**, поэтому ожидание происходит параллельно.

4. После задержки бронь переводится в статус `Confirmed` (или `Rejected` в сценариях отклонения), и заполняется поле `ProcessedAt`.

### Синхронизация при фоновой обработке

При обновлении статусов используется `SemaphoreSlim` (асинхронный аналог мьютекса), чтобы сериализовать критическую секцию записи/обновления состояния в условиях параллельной обработки.

`SemaphoreSlim` используется вместо `lock`, потому что внутри обработки есть `await`, а `lock` нельзя безопасно удерживать вокруг асинхронного кода.

4. При смене статуса заполняется поле ProcessedAt (время обработки).

5. Обновлённая бронь сохраняется обратно в in-memory хранилище.

#### Конкурентность и защита от повторной обработки
Для предотвращения повторной обработки одной и той же брони используется атомарная операция обновления (метод TryProcessPendingAsync), которая переводит бронь из Pending в Confirmed только если она всё ещё находится в статусе Pending на момент обновления.

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

### `lock` (Monitor)

**Где:** `BookingService`

поле:

```csharp
private readonly object _bookingLock = new();
```
и блок:

```csharp
lock (_bookingLock)
{
    // Get event -> TryReserveSeats -> create booking -> store booking
}
```
Зачем нужен: защищает критическую секцию от гонок при создании брони. Без блокировки возможен овербукинг: два потока одновременно видят, что места есть, и оба создают бронь, в результате броней больше, чем мест.

lock охватывает атомарную связку операций:

- получение события;
- проверка/уменьшение AvailableSeats (TryReserveSeats);
- создание и сохранение брони.

Почему lock, а не SemaphoreSlim: внутри секции нет await и I/O — код синхронный и быстрый. lock проще и дешевле по накладным расходам. SemaphoreSlim нужен в основном для асинхронных критических секций, где есть await.

### `lock` в `Event.TryReserveSeats()` и `Event.ReleaseSeats()`
**Где:** Event, поле:
```csharp
private readonly object _seatsLock = new();
```
Зачем нужен: делает операции изменения AvailableSeats потокобезопасными на уровне конкретного события:

TryReserveSeats проверяет и уменьшает AvailableSeats атомарно;
ReleaseSeats увеличивает AvailableSeats атомарно и не даёт превысить TotalSeats.
Это защищает от некорректных значений AvailableSeats при параллельных изменениях.

### `ConcurrentDictionary` как потокобезопасное хранилище
**Где:**

- EventService: ConcurrentDictionary<Guid, Event>
- BookingService: ConcurrentDictionary<Guid, Booking>

Зачем нужен: позволяет безопасно читать/добавлять/обновлять элементы из разных потоков без внешней блокировки на уровне коллекции.

Дополнительно используются атомарные операции словаря:

- TryAdd — безопасное добавление;
- TryRemove — безопасное удаление;
- TryUpdate — атомарное обновление по схеме compare-and-swap (CAS).

### `CAS(Compare-And-Swap)-обновление` (TryUpdate)

**Где:** 
- BookingService.TryProcessPendingAsync

Зачем нужно: гарантирует, что бронь будет обработана (переведена из Pending в Confirmed) только один раз, даже если несколько потоков/тасок пытаются обработать одну и ту же бронь одновременно.

Логика:

- читаем текущую бронь;
- если она Pending, формируем обновлённую;
- выполняем _storage.TryUpdate(id, updated, current) — обновление произойдёт только - если запись не изменилась между чтением и записью;
- если не получилось — повторяем цикл.

### `SemaphoreSlim`
**Где:**
- BookingProcessingBackgroundService

поле:
```csharp
private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
```

Зачем нужен: защищает критическую секцию при фоновой обработке бронирований, где используются await (например, имитация внешнего вызова, обновление статусов).

Почему не lock: lock нельзя безопасно использовать вокруг кода с await, потому что await может “разорвать” выполнение и привести к долгому удержанию блокировки/дедлокам. SemaphoreSlim — асинхронный аналог мьютекса: позволяет await WaitAsync() и гарантированно освобождать ресурс в finally.

Важно: задержка Task.Delay выполняется до захвата семафора, чтобы ожидание внешней системы происходило параллельно, а блокировка удерживалась только на время обновления состояния.