# Лабораторная работа №1 — «Кэширование»

Реализация сервиса генерации данных о товарах на складе с кэшированием ответов через Redis, оркестрацией через .NET Aspire и Blazor WebAssembly клиентом.

## Стек технологий

| Технология | Назначение |
|---|---|
| .NET 8 | Серверная часть |
| ASP.NET Core Minimal API | HTTP-сервис |
| .NET Aspire | Оркестрация сервисов |
| Redis + `IDistributedCache` | Распределённое кэширование |
| Bogus | Генерация тестовых данных |
| Blazor WebAssembly | Клиентское приложение |

## API

**Endpoint**: `GET /api/warehouse-item?id={int}`

При первом запросе генерирует товар и кэширует его. При повторном запросе с тем же `id` возвращает данные из кэша. Время жизни записи в кэше настраивается через `CacheExpirationMinutes` в `appsettings.json` (по умолчанию 10 минут).

**Пример ответа**:

```json
{
  "id": 42,
  "productName": "Fantastic Metal Shoes",
  "category": "Electronics",
  "quantity": 317,
  "pricePerUnit": 1234.56,
  "weightPerUnit": 2.45,
  "dimensions": "30х15х10 см",
  "isFragile": false,
  "lastDeliveryDate": "2024-11-03",
  "nextDeliveryDate": "2025-06-18"
}
```

## Сущность «Товар на складе»

| № | Поле | Тип | Правило генерации |
|---|---|---|---|
| 1 | `id` | `int` | Передаётся из запроса |
| 2 | `productName` | `string` | `Commerce.ProductName()` |
| 3 | `category` | `string` | `Commerce.Categories(1)[0]` |
| 4 | `quantity` | `int` | Случайное `[0, 1000]` |
| 5 | `pricePerUnit` | `decimal` | Случайное `[1, 10000]`, 2 знака |
| 6 | `weightPerUnit` | `double` | Случайное `[0.1, 500]`, 2 знака |
| 7 | `dimensions` | `string` | Формат `**х**х** см` |
| 8 | `isFragile` | `bool` | Случайный `bool` |
| 9 | `lastDeliveryDate` | `DateOnly` | Прошедшая дата ≤ сегодня |
| 10 | `nextDeliveryDate` | `DateOnly` | Дата ≥ `lastDeliveryDate` |

---

# Лабораторная работа №2 — «Балансировка нагрузки»

Реализация API-шлюза на основе Ocelot с кастомным алгоритмом балансировки нагрузки **Weighted Random** (взвешенный случайный выбор).

## Что было сделано

1. **Оркестрация реплик** — в `AppHost.cs` настроен запуск 5 реплик сервиса генерации (`warehouseapp-api-0`…`warehouseapp-api-4`) на портах 7250–7254.
2. **API-шлюз на Ocelot** — проект `Api.Gateway` принимает клиентские запросы и маршрутизирует их к репликам через конфигурацию `ocelot.json`.
3. **Кастомный балансировщик `WeightedRandom`** — реализован алгоритм взвешенного случайного выбора реплики.

## Алгоритм Weighted Random

Каждой реплике присваивается целочисленный вес. При поступлении запроса вероятность выбора реплики пропорциональна её весу:

$$P_i = \frac{w_i}{\sum_{j} w_j}$$

## Конфигурация весов

Веса задаются в `Api.Gateway/appsettings.json` в секции `LoadBalancer:Weights` — массив целых чисел, где индекс соответствует номеру реплики:

```json
{
  "LoadBalancer": {
    "Weights": [ 35, 25, 20, 12, 8 ]
  }
}
```