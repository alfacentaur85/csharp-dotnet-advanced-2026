# EventService (ASP.NET Core Web API)

Простой каркас сервиса событий с in-memory хранилищем, CRUD REST API, валидацией и Swagger.

## Требования
- .NET SDK 8.0+

## Запуск
Из корня проекта:

```bash
dotnet restore
dotnet run
```

## Swagger
В режиме Development доступен Swagger UI:
https://localhost:<port>/swagger

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
Approved — бронь подтверждена (в текущей версии используется как результат фоновой обработки).
Rejected — бронь отклонена (зарезервировано для следующих спринтов).
Cancelled — бронь отменена (зарезервировано для следующих спринтов).

### Эндпоинт бронирования
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
  "status": "Pending"
}

## Формат ответа при ошибках
ибки возвращаются в формате Problem Details (RFC 7807) (Content-Type: application/problem+json).

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

## Тесты
Тесты написаны на xUnit в отдельном проекте (например, EventService.Tests).

Запуск тестов из корня решения/репозитория:

```bash
dotnet test
```