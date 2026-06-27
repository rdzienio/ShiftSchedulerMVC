# ShiftSchedulerMVC

Aplikacja webowa wspierająca tworzenie grafików czasu pracy w zakładach pracy, z automatycznym generowaniem harmonogramów przy użyciu **algorytmu genetycznego**. Obsługuje zmiany 8- i 12-godzinne, wnioski urlopowe, dni świąteczne oraz role użytkowników (administrator, kierownik, pracownik).

## Funkcje

### Administrator
- Zarządzanie użytkownikami (dodawanie, edycja, usuwanie, role, przypisanie kierownika).
- Konfiguracja **godzin rozpoczęcia zmian** (globalnie, z walidacją nakładania w obrębie grup 8h/12h).

### Kierownik
- **Generowanie harmonogramu** algorytmem genetycznym dla wybranego okresu i zapotrzebowania na obsadę.
- Podgląd, edycja i zatwierdzanie harmonogramów (dodawanie/edycja/usuwanie pojedynczych zmian, podgląd miesiąca).
- Zespół pracowników i ich kalendarze.
- Obsługa wniosków urlopowych.
- Nadpisania dla dni świątecznych (osobna obsada, także dla zmian 12h).

### Pracownik
- Mój kalendarz i mój harmonogram.
- Składanie i podgląd wniosków urlopowych.

## Zmiany

| Typ | Długość | Domyślne godziny |
|-----|---------|------------------|
| Poranna | 8h | 06:00–14:00 |
| Popołudniowa | 8h | 14:00–22:00 |
| Nocna | 8h | 22:00–06:00 |
| Dzienna 12h | 12h | 06:00–18:00 |
| Nocna 12h | 12h | 18:00–06:00 |

Zmiany 8h i 12h działają równolegle. Godziny rozpoczęcia są konfigurowalne przez administratora (długość pozostaje stała, koniec = start + długość).

## Algorytm genetyczny

Harmonogram powstaje przez optymalizację z funkcją kary uwzględniającą m.in.:
- pokrycie wymaganej obsady każdej zmiany,
- równomierny rozkład godzin względem celu okresu,
- konflikty z urlopami,
- minimalny odpoczynek dobowy (≥12h) i tygodniowy (≥35h),
- maksymalnie 5 dni pracy z rzędu,
- ciągłość reguł na styku miesięcy (uwzględnienie końcówki poprzedniego grafiku).

Algorytm korzysta z adaptacyjnej mutacji, wczesnego zatrzymania oraz opcjonalnego ziarna (seed) dla powtarzalności wyników.

## Stos technologiczny

- ASP.NET Core MVC (.NET 8, C#)
- Entity Framework Core 8 + SQLite
- ASP.NET Core Identity (uwierzytelnianie i role)
- Bootstrap 5, jQuery (+ walidacja unobtrusive)

## Uruchomienie

Wymagania: **.NET 8 SDK**.

```bash
dotnet run --project ShiftSchedulerMVC
```

Aplikacja domyślnie dostępna pod:
- https://localhost:7062
- http://localhost:5178

Baza SQLite (`ShiftScheduler.sqlite`) jest dołączona z danymi demonstracyjnymi, a migracje EF Core są stosowane automatycznie przy starcie. Przy pierwszym uruchomieniu seedowane są role oraz konto administratora i domyślne godziny zmian.

### Konto administratora (domyślne)

Konfigurowane w `ShiftSchedulerMVC/appsettings.json` (sekcja `AdminAccount`):

- e-mail: `admin@demo.com`
- hasło: `adminAdmin1!`

> Dane logowania to wartości demonstracyjne — zmień je przed użyciem produkcyjnym.

## Struktura projektu

```
ShiftSchedulerMVC/
├─ Controllers/      # Account, Admin, Manager, Schedule, Leave, HolidayOverride, ...
├─ Models/           # encje i modele widoków (m.in. ShiftType, FinalizedSchedule, ShiftTimeSetting)
├─ Services/         # GeneticScheduler – algorytm genetyczny
├─ Helpers/          # ShiftDisplay, ShiftTimes – kody, kolory, godziny zmian
├─ Data/             # ApplicationDbContext, DbInitializer (seed)
├─ Migrations/       # migracje EF Core
└─ Views/            # widoki Razor
```

## Migracje bazy danych

```bash
dotnet ef migrations add NazwaMigracji --project ShiftSchedulerMVC
dotnet ef database update --project ShiftSchedulerMVC
```

Migracje są też stosowane automatycznie przy starcie aplikacji.
