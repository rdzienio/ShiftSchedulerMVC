using ShiftSchedulerMVC.Models;
using System.Diagnostics;

namespace ShiftSchedulerMVC.Services
{
    public static class GeneticScheduler
    {
        public static Chromosome Run(
        List<Employee> employees,
        List<DateTime> dates,
        Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
        int workingHoursReq,
        HashSet<(string empId, DateTime date)> vacationDays,
        int? seed = null,
        IReadOnlyList<(string EmpId, DateTime Date, ShiftType Shift)> priorShifts = null)
        {
            int generations = 100;
            int populationSize = 500;
            // 🔀 Współczynnik krzyżowania. Wartość 0.1 oznaczała, że 90% potomków to czyste
            // klony rodzica - GA zachowywał się bardziej jak wspinaczka z mutacją niż klasyczny
            // algorytm genetyczny (krzyżowanie prawie nie mieszało materiału genetycznego).
            // Typowy zakres dla GA to 0.6-0.9; ustawiamy 0.7 jako rozsądny punkt wyjścia.
            // To parametr do strojenia - warto w pracy pokazać wpływ tej wartości na wyniki.
            double crossoverRate = 0.7;

            // 🎯 Adaptacyjny mutation rate: zamiast jednej stałej wartości dobranej
            // ręcznie dla konkretnego rozmiaru problemu, mutacja rośnie automatycznie,
            // gdy najlepszy fitness "stoi w miejscu" (stagnacja = ryzyko zbieżności
            // przedwczesnej do lokalnego optimum), i wraca do wartości bazowej,
            // gdy GA znowu robi postępy. To eliminuje potrzebę ręcznego dostrajania
            // mutationRate przy każdym nowym zestawie danych.
            const double baseMutationRate = 0.09;
            const double maxMutationRate = 0.45;
            const double mutationRateStep = 0.02;
            double mutationRate = baseMutationRate;

            // 🛑 Early stopping: zamiast jechać sztywno do `generations`, zatrzymujemy się,
            // gdy nie widać postępu przez patience generacji. Próg patience skaluje się
            // z liczbą generacji, żeby nie zatrzymywać się "za szybko" przy małych problemach.
            int patience = Math.Max(15, generations / 5);
            int stagnationCounter = 0;

            int workingHours = workingHoursReq;

            // 🎲 Seed RNG: gdy podany, przebieg GA jest w pełni odtwarzalny (te same dane + ten sam
            // seed → ten sam wynik). Kluczowe przy raportowaniu/porównywaniu eksperymentów w pracy.
            // Gdy null - losowy seed (zachowanie produkcyjne).
            int effectiveSeed = seed ?? Guid.NewGuid().GetHashCode();
            var rng = new Random(effectiveSeed);

            var stopwatch = Stopwatch.StartNew();
            var logPath = Logger.CreateLogFile();

            var population = SmartInitialPopulation(populationSize, dates, employees, shiftRequirements, vacationDays, workingHours, rng, priorShifts);

            Chromosome bestChromosome = null;
            double bestFitness = double.MinValue;

            Logger.Append(logPath, $"workingHours = {workingHours};\ngenerations = {generations};\npopulationSize = {populationSize};\ncrossoverRate = {crossoverRate};\nbaseMutationRate = {baseMutationRate};\npatience = {patience};\nseed = {effectiveSeed};");

            int lastGen = -1;

            for (int gen = 0; gen < generations; gen++)
            {
                lastGen = gen;

                // Jeden przebieg O(n) zamiast pełnego sortu O(n log n): wyłaniamy najlepszego
                // i przy okazji wypełniamy cache fitness dla całej populacji. Selekcja turniejowa
                // i tak losuje kandydatów po indeksach, więc nie wymaga posortowanej listy.
                var currentBest = population[0];
                double currentFitness = GetFitness(currentBest, shiftRequirements, workingHours, vacationDays, employees);
                foreach (var c in population)
                {
                    double f = GetFitness(c, shiftRequirements, workingHours, vacationDays, employees);
                    if (f > currentFitness)
                    {
                        currentFitness = f;
                        currentBest = c;
                    }
                }

                bool improved = currentFitness > bestFitness;

                if (improved)
                {
                    bestFitness = currentFitness;
                    bestChromosome = CloneChromosome(currentBest);
                    stagnationCounter = 0;
                    mutationRate = baseMutationRate; // wracamy do bazowej mutacji - GA znowu robi postępy
                }
                else
                {
                    stagnationCounter++;
                    // Im dłużej brak postępu, tym wyższa mutacja (do limitu maxMutationRate),
                    // żeby zwiększyć różnorodność i wyjść z lokalnego optimum.
                    mutationRate = Math.Min(maxMutationRate, baseMutationRate + stagnationCounter * mutationRateStep);
                }

                Logger.Append(logPath, $"Generation {gen + 1}: Best Fitness = {currentFitness:F5}, MutationRate = {mutationRate:F3}, Stagnation = {stagnationCounter}");

                // 🛑 Early stopping - brak postępu przez `patience` generacji
                if (stagnationCounter >= patience)
                {
                    Logger.Append(logPath, $"Early stopping: brak poprawy fitness przez {patience} generacji (generacja {gen + 1}).");
                    break;
                }

                var nextGeneration = new List<Chromosome> { currentBest };

                while (nextGeneration.Count < populationSize)
                {
                    var parent1 = TournamentSelection(population, shiftRequirements, 5, rng, workingHours, vacationDays, employees);
                    var parent2 = TournamentSelection(population, shiftRequirements, 5, rng, workingHours, vacationDays, employees);

                    Chromosome child = rng.NextDouble() < crossoverRate
                        ? Crossover(parent1, parent2, rng)
                        : CloneChromosome(parent1);

                    if (rng.NextDouble() < mutationRate)
                        Mutate(child, employees, rng);

                    Repair(child, shiftRequirements, employees, vacationDays, workingHours, rng, priorShifts);
                    nextGeneration.Add(child);
                }

                population = nextGeneration;
            }

            stopwatch.Stop();
            Logger.Append(logPath, $"Total runtime: {stopwatch.Elapsed.TotalSeconds:F2} seconds (generations run: {lastGen + 1}/{generations})");

            // 📝 Dodatkowy log tylko dla najlepszego chromosomu
            var errorLog = EvaluateFitnessWithErrors(bestChromosome, shiftRequirements, workingHours, vacationDays, employees);
            Logger.Append(logPath, "--- Final Best Chromosome Diagnostics ---");
            foreach (var line in errorLog)
                Logger.Append(logPath, line);
            Logger.Append(logPath, $"Best Chromosome: {EvaluateFitness(bestChromosome, shiftRequirements, workingHours, vacationDays, employees)}");
            Console.WriteLine($"Best Chromosome: {EvaluateFitness(bestChromosome, shiftRequirements, workingHours, vacationDays, employees)}");
            return bestChromosome;
        }


        // 🎯 Bazowe wagi kar - WZGLĘDNE wagi ważności (proporcje), nie wartości absolutne.
        // Każda kara jest normalizowana względem rozmiaru problemu (liczba pracowników,
        // liczba dni, liczba wymaganych obsad) PRZED przemnożeniem przez wagę, dzięki czemu
        // te same wagi działają sensownie niezależnie od tego, czy planujemy 5 czy 50
        // pracowników, 7 czy 31 dni. To eliminuje potrzebę ręcznego przestrajania co miesiąc.
        private static class FitnessWeights
        {
            // ⚖️ Wagi EFEKTYWNE (rzeczywiste). Wcześniej do sumy kar doklejone były inline
            // mnożniki (*100, *10, brak), przez co zadeklarowane wagi nie odpowiadały temu,
            // co naprawdę dzieje się w fitness. Mnożniki zostały wciągnięte tutaj, więc poniższe
            // liczby są dokładnie tym, czym ważona jest każda (znormalizowana) kategoria kar.
            // Zachowanie GA jest identyczne jak poprzednio - zmieniła się tylko czytelność.
            public const double Coverage = 300.0;          // niedobór obsady zmiany (powinno być rzadkie po Repair)
            public const double VacationConflict = 500.0;  // przypisanie w dniu urlopu (nie powinno się zdarzyć po Repair)
            public const double HoursBalance = 100.0;      // odchylenie od docelowej liczby godzin (soft - sprawiedliwość)
            public const double DoubleShift = 200.0;        // więcej niż 1 zmiana dziennie (nie powinno się zdarzyć po Repair)
            // ⚠️ LongStretch i ShortRestDaily mają niskie wagi, bo ich kara liczona jest w INNEJ
            // jednostce niż reszta (LongStretch = nadmiarowe godziny >40h, czyli duże wartości;
            // pozostałe = zliczone incydenty po ~1). Niska waga kompensuje większą skalę surowej kary.
            // Przy ewentualnym strojeniu trzeba pamiętać o tej różnicy jednostek.
            public const double LongStretch = 1.5;          // >40h pracy w ciągłym okresie bez 12h przerwy (kara w godzinach)
            public const double ShortRestDaily = 10.0;      // <12h przerwy między zmianami
            public const double MaxConsecutiveDays = 150.0; // >5 dni z rzędu (nie powinno się zdarzyć po Repair)
            public const double WeeklyRest = 150.0;         // <35h odpoczynku tygodniowego (nie powinno się zdarzyć po Repair)
        }

        // 🚀 Zwraca fitness z cache, licząc go tylko raz na chromosom. Wartości
        // wymagań/godzin/urlopów są stałe w obrębie jednego uruchomienia Run(), więc
        // cache jest ważny dopóki geny się nie zmienią (Mutate/Crossover/Repair czyszczą cache).
        private static double GetFitness(
            Chromosome chrom,
            Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
            int workingHours,
            HashSet<(string empId, DateTime date)> vacationDays,
            List<Employee> employees)
        {
            return chrom.CachedFitness ??=
                EvaluateFitness(chrom, shiftRequirements, workingHours, vacationDays, employees);
        }

        private static double EvaluateFitness(
    Chromosome chrom,
    Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
    int workingHours,
    HashSet<(string empId, DateTime date)> vacationDays,
    List<Employee> employees,
    List<string> log = null)
        {
            // `log` jest opcjonalny: gdy != null, zbieramy do niego czytelne komunikaty o naruszeniach
            // (używane przez EvaluateFitnessWithErrors do diagnostyki). Gdy null - liczymy sam fitness.

            // Kary są zbierane OSOBNO per kategoria, a normalizowane (dzielone przez
            // odpowiednią miarę rozmiaru problemu) na końcu - PRZED przemnożeniem przez
            // wagę kategorii. To jest kluczowe: jeśli policzymy "kara_na_incydent / N * waga * N",
            // N się zniesie i normalizacja nic nie zmieni. Tutaj liczymy
            // "SUMA_kar_kategorii / N", więc N realnie dzieli całkowity wpływ tej kategorii
            // na fitness - i to jest to, co chcemy: ta sama waga ma sens niezależnie od tego,
            // czy grafik ma 5 czy 50 pracowników / 7 czy 31 dni.
            double penaltyCoverage = 0;
            double penaltyVacation = 0;
            double penaltyHours = 0;
            double penaltyDoubleShift = 0;
            double penaltyLongStretch = 0;
            double penaltyShortRest = 0;
            double penaltyConsecutiveDays = 0;
            double penaltyWeeklyRest = 0;

            // Inicjalizacja od PEŁNEJ listy pracowników: dzięki temu pracownik bez żadnego
            // przydziału (0 zmian i 0 urlopu) jest widoczny i karany za niedobór godzin,
            // a mianownik normalizacji (employeeCount) obejmuje wszystkich - inaczej GA mógłby
            // bezkarnie zostawić kogoś całkiem poza grafikiem.
            var employeeHours = employees.ToDictionary(e => e.Id, _ => 0);
            var dailyAssignments = new Dictionary<(string, DateTime), int>();
            var shiftHistory = new Dictionary<string, List<(DateTime, ShiftType)>>();

            foreach (var gene in chrom.Genes)
            {
                if (!shiftRequirements.TryGetValue(gene.Date.Date, out var dayMap) ||
                    !dayMap.TryGetValue(gene.Shift, out int required))
                    continue;

                int assigned = gene.AssignedEmployees.Count;

                if (assigned < required)
                {
                    penaltyCoverage += (required - assigned);
                    log?.Add($"KARA: Za mało osób na zmianie {gene.Date:yyyy-MM-dd} [{gene.Shift}]");
                }

                foreach (var emp in gene.AssignedEmployees)
                {
                    if (!employeeHours.ContainsKey(emp.Id)) employeeHours[emp.Id] = 0;
                    employeeHours[emp.Id] += 8;

                    var key = (emp.Id, gene.Date.Date);
                    if (!dailyAssignments.ContainsKey(key)) dailyAssignments[key] = 0;
                    dailyAssignments[key]++;

                    if (!shiftHistory.ContainsKey(emp.Id)) shiftHistory[emp.Id] = new();
                    shiftHistory[emp.Id].Add((gene.Date, gene.Shift));

                    if (vacationDays.Contains((emp.Id, gene.Date.Date)))
                    {
                        penaltyVacation += 1;
                        log?.Add($"❌ BŁĄD: Pracownik {emp.Id} został przypisany do zmiany {gene.Shift} w dzień urlopu {gene.Date:yyyy-MM-dd}");
                    }
                }
            }

            foreach (var (empId, date) in vacationDays)
            {
                if (!employeeHours.ContainsKey(empId))
                    employeeHours[empId] = 0;

                employeeHours[empId] += 8;
            }

            // 📏 Skala problemu - dzielniki normalizujące poszczególne kategorie kar.
            int employeeCount = Math.Max(1, employeeHours.Count);
            int totalShiftSlots = Math.Max(1, shiftRequirements.SelectMany(d => d.Value.Values).Count(v => v > 0));

            foreach (var kvp in employeeHours)
            {
                if (kvp.Value == workingHours) continue;

                // Wyrażone w % odchylenia od celu (nie w godzinach absolutnych), więc skaluje
                // się automatycznie razem ze zmianą workingHours między miesiącami.
                double deviationRatio = Math.Abs(kvp.Value - workingHours) / (double)workingHours;
                penaltyHours += kvp.Value > workingHours ? deviationRatio * 0.5 : deviationRatio * 0.25;

                if (log != null)
                    log.Add(kvp.Value > workingHours
                        ? $"Pracownik {kvp.Key} przekroczył limit godzin ({kvp.Value}h)"
                        : $"Pracownik {kvp.Key} nie spełnił limitu godzin ({kvp.Value}h)");
            }

            foreach (var kvp in dailyAssignments)
            {
                if (kvp.Value > 1)
                {
                    penaltyDoubleShift += (kvp.Value - 1);
                    log?.Add($"KARA: Pracownik {kvp.Key.Item1} ma więcej niż jedną zmianę w dniu {kvp.Key.Item2:yyyy-MM-dd}");
                }
            }

            foreach (var kvp in shiftHistory)
            {
                var shifts = kvp.Value.OrderBy(s => s.Item1).ToList();
                if (shifts.Count == 0) continue;

                double consecutiveHours = 0;
                DateTime? lastEnd = null;

                for (int i = 0; i < shifts.Count; i++)
                {
                    DateTime shiftStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    DateTime shiftEnd = GetShiftEnd(shifts[i].Item1, shifts[i].Item2);

                    if (lastEnd == null || (shiftStart - lastEnd.Value).TotalHours <= 12)
                    {
                        consecutiveHours += (shiftEnd - shiftStart).TotalHours;
                    }
                    else
                    {
                        consecutiveHours = (shiftEnd - shiftStart).TotalHours;
                    }

                    lastEnd = shiftEnd;

                    if (consecutiveHours > 40)
                    {
                        penaltyLongStretch += (consecutiveHours - 40);
                        log?.Add($"KARA: Pracownik {kvp.Key} przepracował ciągiem {consecutiveHours}h bez dnia wolnego");
                    }
                }

                for (int i = 1; i < shifts.Count; i++)
                {
                    var prevEnd = GetShiftEnd(shifts[i - 1].Item1, shifts[i - 1].Item2);
                    var currStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    var rest = (currStart - prevEnd).TotalHours;
                    if (rest < 12)
                    {
                        penaltyShortRest += 1;
                        log?.Add($"KARA: Pracownik {kvp.Key} miał za krótką przerwę ({rest:0.##}h) między zmianami");
                    }
                }

                // --- Maksymalnie 5 dni roboczych pod rząd (safety net - po Repair rzadkie) ---
                var shiftDays = kvp.Value
                    .Select(s => s.Item1.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                int consecutiveWorkDays = 1;

                for (int i = 1; i < shiftDays.Count; i++)
                {
                    if ((shiftDays[i] - shiftDays[i - 1]).TotalDays == 1)
                    {
                        consecutiveWorkDays++;
                        if (consecutiveWorkDays > 5)
                        {
                            penaltyConsecutiveDays += 1;
                            log?.Add($"KARA: Pracownik {kvp.Key} pracował {consecutiveWorkDays} dni z rzędu bez dnia wolnego");
                            break;
                        }
                    }
                    else
                    {
                        consecutiveWorkDays = 1;
                    }
                }

                var shiftPeriods = shifts.Select(s =>
                {
                    DateTime start = GetShiftStart(s.Item1, s.Item2);
                    DateTime end = GetShiftEnd(s.Item1, s.Item2);
                    return (Start: start, End: end);
                }).OrderBy(p => p.Start).ToList();

                DateTime weekStart = shiftPeriods.First().Start.Date;
                DateTime weekEnd = weekStart.AddDays(7);

                while (weekStart <= shiftPeriods.Last().End)
                {
                    if (weekEnd > shiftPeriods.Last().End) break;
                    var shiftsThisWeek = shiftPeriods
                        .Where(p => p.Start >= weekStart && p.Start <= weekEnd)
                        .ToList();

                    if (shiftsThisWeek.Count >= 2)
                    {
                        // Najdłuższy odpoczynek w tygodniu liczony z UWZGLĘDNIENIEM wolnego na
                        // brzegach tygodnia (przed pierwszą i po ostatniej zmianie) - inaczej tydzień
                        // z pracą tylko na początku, a wolnym do końca, byłby fałszywie karany.
                        // Spójne z LongestRestInTrailingWeek używanym w Repair.
                        double maxBreak = Math.Max(
                            (shiftsThisWeek.First().Start - weekStart).TotalHours,   // wolne na początku tygodnia
                            (weekEnd - shiftsThisWeek.Last().End).TotalHours);       // wolne na końcu tygodnia
                        for (int i = 1; i < shiftsThisWeek.Count; i++)
                        {
                            var previous = shiftsThisWeek[i - 1];
                            var current = shiftsThisWeek[i];

                            double breakHours = (current.Start - previous.End).TotalHours;
                            if (breakHours > maxBreak)
                                maxBreak = breakHours;
                        }

                        if (maxBreak < 35)
                        {
                            penaltyWeeklyRest += 1;
                            log?.Add($"KARA: Pracownik {kvp.Key} nie miał 35h odpoczynku tygodniowego (tydzień od {weekStart:yyyy-MM-dd})");
                        }
                    }

                    weekStart = weekStart.AddDays(7);
                    weekEnd = weekStart.AddDays(7);
                }
            }

            // 🎯 Normalizacja: każda kategoria kar dzielona przez WŁASNY rozmiar problemu,
            // PRZED przemnożeniem przez wagę. Tu N faktycznie się nie znosi, bo dzielimy
            // sumę zliczonych incydentów (nie incydent-razy-N), więc wynik realnie zależy
            // od tego, jaki % obsady/pracowników/dni jest objęty naruszeniem.
            double totalPenalty =
                FitnessWeights.Coverage * (penaltyCoverage / totalShiftSlots) +
                FitnessWeights.VacationConflict * (penaltyVacation / employeeCount) +
                FitnessWeights.HoursBalance * (penaltyHours / employeeCount) +
                FitnessWeights.DoubleShift * (penaltyDoubleShift / employeeCount) +
                FitnessWeights.LongStretch * (penaltyLongStretch / employeeCount) +
                FitnessWeights.ShortRestDaily * (penaltyShortRest / employeeCount) +
                FitnessWeights.MaxConsecutiveDays * (penaltyConsecutiveDays / employeeCount) +
                FitnessWeights.WeeklyRest * (penaltyWeeklyRest / employeeCount);

            // Fitness w (0,1]: brak kar → 1.0; im więcej kar, tym bliżej 0. Monotonicznie malejące
            // względem totalPenalty, więc ranking osobników zależy wyłącznie od sumy kar.
            double fitness = 1.0 / (1.0 + totalPenalty);
            return fitness;
        }



        private static void Mutate(Chromosome chrom, List<Employee> employees, Random rng)
        {
            int geneIndex = rng.Next(chrom.Genes.Count);
            var gene = chrom.Genes[geneIndex];

            if (gene.AssignedEmployees.Count > 0)
            {
                if (rng.NextDouble() < 0.5)
                {
                    int count = gene.AssignedEmployees.Count;
                    gene.AssignedEmployees.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        var newEmp = employees[rng.Next(employees.Count)];
                        gene.AssignedEmployees.Add(newEmp);
                    }
                }
                else
                {
                    int index = rng.Next(gene.AssignedEmployees.Count);
                    gene.AssignedEmployees[index] = employees[rng.Next(employees.Count)];
                }
            }
            chrom.CachedFitness = null; // geny się zmieniły → wymuś przeliczenie fitness
        }

        private static Chromosome Crossover(Chromosome p1, Chromosome p2, Random rng)
        {
            var child = new Chromosome();

            // Losowy punkt podziału zamiast stałej połowy: stały punkt zawsze tnie geny w tym
            // samym miejscu, więc początek harmonogramu zawsze pochodził od p1, a koniec od p2.
            // Losowy punkt pozwala faktycznie mieszać materiał genetyczny w różnych konfiguracjach.
            // Zakres [1, count) gwarantuje, że dziecko dostaje fragment od OBOJGA rodziców.
            int split = p1.Genes.Count > 1 ? rng.Next(1, p1.Genes.Count) : p1.Genes.Count;

            for (int i = 0; i < p1.Genes.Count; i++)
            {
                var gene = i < split ? p1.Genes[i] : p2.Genes[i];
                child.Genes.Add(new Gene
                {
                    Date = gene.Date,
                    Shift = gene.Shift,
                    AssignedEmployees = new List<Employee>(gene.AssignedEmployees)
                });
            }
            return child; // nowy chromosom → CachedFitness domyślnie null
        }

        private static Chromosome TournamentSelection(
            List<Chromosome> population,
            Dictionary<DateTime, Dictionary<ShiftType, int>> requirements,
            int size,
            Random rng,
            int workingHours,
            HashSet<(string empId, DateTime date)> vacationDays,
            List<Employee> employees)
        {
            var candidates = new List<Chromosome>();
            for (int i = 0; i < size; i++)
                candidates.Add(population[rng.Next(population.Count)]);

            return candidates
                .OrderByDescending(c => GetFitness(c, requirements, workingHours, vacationDays, employees))
                .First();
        }


        private static Chromosome CloneChromosome(Chromosome source)
        {
            return new Chromosome
            {
                Genes = source.Genes.Select(g => new Gene
                {
                    Date = g.Date,
                    Shift = g.Shift,
                    AssignedEmployees = new List<Employee>(g.AssignedEmployees)
                }).ToList()
            };
        }

        // 🔧 Stan "historii pracy" pracownika, używany przez Repair() do wykrywania
        // złamania reguł sekwencyjnych (5 dni z rzędu, 35h odpoczynku tygodniowego),
        // które wymagają znajomości KOLEJNYCH dni, a nie tylko jednej poprzedniej zmiany.
        private class EmployeeWorkState
        {
            public List<DateTime> WorkedDays = new(); // unikalne dni pracy, chronologicznie
            public int ConsecutiveDays = 0;
        }

        private static bool WouldExceedMaxConsecutiveDays(
            string empId,
            DateTime date,
            Dictionary<string, EmployeeWorkState> workState,
            int maxConsecutiveDays = 5)
        {
            if (!workState.TryGetValue(empId, out var state) || state.WorkedDays.Count == 0)
                return false;

            var lastDay = state.WorkedDays.Last();

            // Jeśli ten dzień jest bezpośrednią kontynuacją (poprzedni dzień + 1)
            if ((date.Date - lastDay).Days == 1)
                return state.ConsecutiveDays >= maxConsecutiveDays;

            return false; // był dzień przerwy, licznik się zresetuje
        }

        private static void RegisterWorkedDay(string empId, DateTime date, Dictionary<string, EmployeeWorkState> workState)
        {
            if (!workState.TryGetValue(empId, out var state))
            {
                state = new EmployeeWorkState();
                workState[empId] = state;
            }

            if (state.WorkedDays.Count > 0 && state.WorkedDays.Last().Date == date.Date)
                return; // ten dzień już zarejestrowany

            if (state.WorkedDays.Count > 0 && (date.Date - state.WorkedDays.Last()).Days == 1)
                state.ConsecutiveDays++;
            else
                state.ConsecutiveDays = 1;

            state.WorkedDays.Add(date.Date);
        }

        // 🗓️ Zasiewa stan reguł sekwencyjnych zmianami z POPRZEDNIEGO miesiąca (ostatnie ~7 dni),
        // żeby grafik na styku miesięcy respektował: 35h odpoczynku (shiftHistory), max 5 dni
        // z rzędu (workState) oraz zgodność zmian / po nocnej tylko nocna (lastShiftMap).
        // Dowolny z trzech słowników może być null (np. SmartInitialPopulation sieje tylko lastShiftMap).
        private static void SeedFromPriorShifts(
            IEnumerable<(string EmpId, DateTime Date, ShiftType Shift)> priorShifts,
            Dictionary<string, (DateTime date, ShiftType shift)> lastShiftMap,
            Dictionary<string, EmployeeWorkState> workState,
            Dictionary<string, List<(DateTime date, ShiftType shift)>> shiftHistory)
        {
            if (priorShifts == null) return;

            // Chronologicznie - by RegisterWorkedDay poprawnie policzył ciąg dni, a lastShiftMap
            // skończył na NAJPÓŹNIEJSZEJ zmianie sprzed horyzontu.
            foreach (var ps in priorShifts.OrderBy(p => p.Date).ThenBy(p => p.Shift))
            {
                if (shiftHistory != null)
                {
                    if (!shiftHistory.TryGetValue(ps.EmpId, out var hist)) { hist = new(); shiftHistory[ps.EmpId] = hist; }
                    hist.Add((ps.Date, ps.Shift));
                }
                if (workState != null)
                    RegisterWorkedDay(ps.EmpId, ps.Date, workState);
                if (lastShiftMap != null)
                    lastShiftMap[ps.EmpId] = (ps.Date, ps.Shift);
            }
        }

        // 🛌 Najdłuższy ciągły odpoczynek (w godzinach) w 7-dniowym oknie kończącym się zmianą
        // (date, shift). KLUCZOWE: liczymy też wolne PRZED pierwszą zmianą w oknie (od początku
        // okna). Bez tego dwie zmiany w kolejnych dniach dawały jedyną przerwę < 35h i reguła
        // fałszywie zgłaszała naruszenie na starcie horyzontu (stąd np. pusta nocna 2. dnia).
        //
        // 🔌 Punkt rozszerzenia o POPRZEDNIE MIESIĄCE: `shifts` to pełna historia zmian pracownika.
        // Gdy zasiejemy ją zmianami z końca poprzedniego miesiąca, te mieszczące się w oknie 7 dni
        // zostaną tu uwzględnione automatycznie - żadnej zmiany w tej metodzie nie trzeba będzie robić.
        private static double LongestRestInTrailingWeek(
            DateTime date,
            ShiftType shift,
            IEnumerable<(DateTime date, ShiftType shift)> shifts)
        {
            DateTime windowStart = date.Date.AddDays(-6); // początek 7-dniowego okna [date-6 .. date]

            var periods = shifts
                .Where(h => (date.Date - h.date.Date).Days >= 0 && (date.Date - h.date.Date).Days < 7)
                .Select(h => (Start: GetShiftStart(h.date, h.shift), End: GetShiftEnd(h.date, h.shift)))
                .Append((Start: GetShiftStart(date, shift), End: GetShiftEnd(date, shift)))
                .OrderBy(p => p.Start)
                .ToList();

            // Przerwa wiodąca: od początku okna do pierwszej zmiany.
            double maxBreak = (periods[0].Start - windowStart).TotalHours;
            for (int i = 1; i < periods.Count; i++)
                maxBreak = Math.Max(maxBreak, (periods[i].Start - periods[i - 1].End).TotalHours);

            return maxBreak;
        }

        // Sprawdza, czy przydzielenie tej zmiany złamałoby zasadę 35h nieprzerwanego
        // odpoczynku w 7-dniowym oknie. Patrz LongestRestInTrailingWeek (tam jest cała logika
        // okna + hook na poprzednie miesiące).
        private static bool WouldViolateWeeklyRest(
            string empId,
            DateTime date,
            ShiftType shift,
            Dictionary<string, List<(DateTime date, ShiftType shift)>> shiftHistory)
        {
            var shifts = shiftHistory.TryGetValue(empId, out var history)
                ? (IEnumerable<(DateTime date, ShiftType shift)>)history
                : Array.Empty<(DateTime date, ShiftType shift)>();

            return LongestRestInTrailingWeek(date, shift, shifts) < 35;
        }

        private static void Repair(
    Chromosome chrom,
    Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
    List<Employee> employees,
    HashSet<(string empId, DateTime date)> vacationDays,
    int workingHours,
    Random rng,
    IReadOnlyList<(string EmpId, DateTime Date, ShiftType Shift)> priorShifts = null)
        {
            chrom.CachedFitness = null; // Repair modyfikuje przypisania → unieważnij cache
            var employeeHours = employees.ToDictionary(e => e.Id, _ => 0);
            var dailyAssignments = new HashSet<(string EmpId, DateTime Date)>();
            var lastShiftMap = new Dictionary<string, (DateTime date, ShiftType shift)>();
            var workState = new Dictionary<string, EmployeeWorkState>();
            var shiftHistory = new Dictionary<string, List<(DateTime date, ShiftType shift)>>();

            // 🗓️ Zasianie stanu zmianami z końca poprzedniego miesiąca (jeśli przekazane), zanim
            // ruszymy po bieżącym horyzoncie. Dzięki temu reguły sekwencyjne działają przez granicę
            // miesięcy: 35h odpoczynku, max 5 dni z rzędu i zgodność zmian (po nocnej tylko nocna).
            SeedFromPriorShifts(priorShifts, lastShiftMap, workState, shiftHistory);

            // Urlopy
            foreach (var (empId, date) in vacationDays)
                if (employeeHours.ContainsKey(empId))
                    employeeHours[empId] += 8;

            // Repair przechodzi po genach CHRONOLOGICZNIE, żeby reguły sekwencyjne
            // (5 dni z rzędu, 35h odpoczynku) miały sens - kolejność dat musi być zachowana,
            // niezależnie od kolejności genów w chrom.Genes (po Crossover/Mutate może być inna).
            var orderedGenes = chrom.Genes.OrderBy(g => g.Date).ThenBy(g => g.Shift).ToList();

            // Inicjalizacja stanu z chromosomu
            foreach (var gene in orderedGenes)
            {
                foreach (var emp in gene.AssignedEmployees.ToList())
                {
                    var date = gene.Date.Date;
                    var key = (emp.Id, date);

                    // 🧹 Sprawdzenie błędnych przypisań: duplikat dnia, urlop
                    if (dailyAssignments.Contains(key) || vacationDays.Contains(key))
                    {
                        gene.AssignedEmployees.Remove(emp);
                        continue;
                    }

                    // 🧪 Sprawdzenie zgodności z poprzednią zmianą (np. nocna -> ranna)
                    if (!IsEligibleAfterPreviousShift(emp.Id, gene.Date, gene.Shift, lastShiftMap))
                    {
                        gene.AssignedEmployees.Remove(emp);
                        continue;
                    }

                    // 🧮 Limit godzin pracy (hard constraint, nie tylko kara w fitness)
                    if (employeeHours[emp.Id] + 8 > workingHours)
                    {
                        gene.AssignedEmployees.Remove(emp);
                        continue;
                    }

                    // 📅 Max 5 dni roboczych z rzędu
                    if (WouldExceedMaxConsecutiveDays(emp.Id, date, workState))
                    {
                        gene.AssignedEmployees.Remove(emp);
                        continue;
                    }

                    // 😴 Min. 35h nieprzerwanego odpoczynku w tygodniu
                    if (WouldViolateWeeklyRest(emp.Id, date, gene.Shift, shiftHistory))
                    {
                        gene.AssignedEmployees.Remove(emp);
                        continue;
                    }

                    employeeHours[emp.Id] += 8;
                    dailyAssignments.Add(key);
                    lastShiftMap[emp.Id] = (gene.Date, gene.Shift);
                    RegisterWorkedDay(emp.Id, date, workState);

                    if (!shiftHistory.ContainsKey(emp.Id)) shiftHistory[emp.Id] = new();
                    shiftHistory[emp.Id].Add((gene.Date, gene.Shift));
                }
            }

            // Uzupełnianie niedoborów - chronologicznie, z tych samych powodów co wyżej
            foreach (var gene in orderedGenes)
            {
                var date = gene.Date.Date;

                if (!shiftRequirements.TryGetValue(date, out var dayMap) ||
                    !dayMap.TryGetValue(gene.Shift, out int required))
                    continue;

                if (required == 0)
                {
                    gene.AssignedEmployees.Clear();
                    continue;
                }

                while (gene.AssignedEmployees.Count < required)
                {
                    var available = employees
                        .Where(e =>
                            !vacationDays.Contains((e.Id, date)) &&
                            !dailyAssignments.Contains((e.Id, date)) &&
                            employeeHours[e.Id] + 8 <= workingHours &&
                            IsEligibleAfterPreviousShift(e.Id, gene.Date, gene.Shift, lastShiftMap) &&
                            !WouldExceedMaxConsecutiveDays(e.Id, date, workState) &&
                            !WouldViolateWeeklyRest(e.Id, date, gene.Shift, shiftHistory))
                        .OrderBy(_ => rng.Next())
                        .ToList();

                    if (!available.Any()) break;

                    var emp = available.First();
                    gene.AssignedEmployees.Add(emp);
                    employeeHours[emp.Id] += 8;
                    dailyAssignments.Add((emp.Id, date));
                    lastShiftMap[emp.Id] = (gene.Date, gene.Shift);
                    RegisterWorkedDay(emp.Id, date, workState);
                    if (!shiftHistory.ContainsKey(emp.Id)) shiftHistory[emp.Id] = new();
                    shiftHistory[emp.Id].Add((gene.Date, gene.Shift));
                }

                // Opcjonalnie nadmiarowy
                if (gene.AssignedEmployees.Count == required)
                {
                    // Tylko jeśli wciąż mamy znaczną liczbę dostępnych ludzi
                    var potentialExtras = employees
                        .Where(e =>
                            !vacationDays.Contains((e.Id, date)) &&
                            !dailyAssignments.Contains((e.Id, date)) &&
                            employeeHours[e.Id] + 8 <= workingHours &&
                            IsEligibleAfterPreviousShift(e.Id, gene.Date, gene.Shift, lastShiftMap) &&
                            !WouldExceedMaxConsecutiveDays(e.Id, date, workState) &&
                            !WouldViolateWeeklyRest(e.Id, date, gene.Shift, shiftHistory))
                        // Nadmiarowego dobieramy wg NAJMNIEJSZEJ liczby godzin (a nie losowo):
                        // spare-slot ma uzupełniać braki, więc ma trafiać do najbardziej zaległego
                        // pracownika. Losowy tie-break zachowuje resztki różnorodności.
                        .OrderBy(e => employeeHours[e.Id])
                        .ThenBy(_ => rng.Next())
                        .ToList();

                    // Gwardia `> 1`: dodajemy nadmiarowego TYLKO gdy zostaje ≥1 osoba w zapasie.
                    // Zostawienie bufora jest kluczowe - bez niego Repair zbyt łapczywie dorzuca
                    // nadmiarowych, zużywa budżet godzin i zagładza późniejsze WYMAGANE zmiany
                    // (coverage waży 300, więc taki "dopchany" grafik ma gorszy fitness). Empirycznie
                    // `> 1` daje pełne pokrycie + wyrównane godziny; `>= 1` psuje pokrycie.
                    if (potentialExtras.Count > 1)
                    {
                        var extra = potentialExtras.First();
                        gene.AssignedEmployees.Add(extra);
                        employeeHours[extra.Id] += 8;
                        dailyAssignments.Add((extra.Id, date));
                        lastShiftMap[extra.Id] = (gene.Date, gene.Shift);
                        RegisterWorkedDay(extra.Id, date, workState);
                        if (!shiftHistory.ContainsKey(extra.Id)) shiftHistory[extra.Id] = new();
                        shiftHistory[extra.Id].Add((gene.Date, gene.Shift));
                    }
                }

            }
        }




        private static DateTime GetShiftStart(DateTime date, ShiftType shift)
        {
            return shift switch
            {
                ShiftType.Morning => date.Date.AddHours(6),
                ShiftType.Afternoon => date.Date.AddHours(14),
                ShiftType.Night => date.Date.AddHours(22),
                _ => date
            };
        }

        private static DateTime GetShiftEnd(DateTime date, ShiftType shift)
        {
            return shift switch
            {
                ShiftType.Morning => date.Date.AddHours(14),
                ShiftType.Afternoon => date.Date.AddHours(22),
                ShiftType.Night => date.Date.AddDays(1).AddHours(6),
                _ => date
            };
        }

        private static List<Chromosome> SmartInitialPopulation(
    int populationSize,
    List<DateTime> dates,
    List<Employee> employees,
    Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
    HashSet<(string empId, DateTime date)> vacationDays,
    int workingHours,
    Random rng,
    IReadOnlyList<(string EmpId, DateTime Date, ShiftType Shift)> priorShifts = null)
        {
            var population = new List<Chromosome>();
            var periodEnd = dates.Max();

            for (int p = 0; p < populationSize; p++)
            {
                var chrom = new Chromosome();
                var employeeHours = employees.ToDictionary(e => e.Id, e => 0);
                var dailyAssignments = new HashSet<(string EmpId, DateTime Date)>();
                var lastShiftMap = new Dictionary<string, (DateTime date, ShiftType shift)>();

                // 🗓️ Zasiej zgodność zmian z poprzedniego miesiąca (po nocnej tylko nocna itd.),
                // by populacja startowa nie tworzyła kolizji na styku miesięcy. Reguły 35h i 5 dni
                // z rzędu sprawdza dopiero Repair, więc tu sieją tylko lastShiftMap.
                SeedFromPriorShifts(priorShifts, lastShiftMap, null, null);

                // Zlicz godziny z zatwierdzonych urlopów
                foreach (var (empId, date) in vacationDays)
                {
                    if (employeeHours.ContainsKey(empId))
                        employeeHours[empId] += 8;
                }

                foreach (var date in dates.OrderByDescending(d => GetDayWeight(d, periodEnd)))
                {
                    foreach (ShiftType shift in Enum.GetValues(typeof(ShiftType)))
                    {
                        var gene = new Gene { Date = date, Shift = shift };
                        chrom.Genes.Add(gene);

                        if (!shiftRequirements.TryGetValue(date, out var dayReqs) ||
                            !dayReqs.TryGetValue(shift, out int required))
                            continue;

                        int attempts = 0;
                        while (gene.AssignedEmployees.Count < required && attempts < 100)
                        {
                            var emp = employees[rng.Next(employees.Count)];
                            var key = (emp.Id, date.Date);

                            if (vacationDays.Contains((emp.Id, date.Date))) { attempts++; continue; }
                            if (dailyAssignments.Contains(key)) { attempts++; continue; }
                            if (employeeHours[emp.Id] + 8 > workingHours) { attempts++; continue; }
                            if (!IsEligibleAfterPreviousShift(emp.Id, date, shift, lastShiftMap)) { attempts++; continue; }

                            gene.AssignedEmployees.Add(emp);
                            employeeHours[emp.Id] += 8;
                            dailyAssignments.Add(key);
                            lastShiftMap[emp.Id] = (date, shift);
                            attempts++;
                        }

                        // Opcjonalnie dodanie 1 nadmiarowej osoby (jeśli jest zapas godzin)
                        if (gene.AssignedEmployees.Count == required)
                        {
                            // Tylko jeśli wciąż mamy znaczną liczbę dostępnych ludzi
                            var potentialExtras = employees
                                .Where(e =>
                                    !vacationDays.Contains((e.Id, date)) &&
                                    !dailyAssignments.Contains((e.Id, date)) &&
                                    employeeHours[e.Id] + 8 <= workingHours &&
                                    IsEligibleAfterPreviousShift(e.Id, gene.Date, gene.Shift, lastShiftMap))
                                .OrderBy(_ => rng.Next())
                                .ToList();

                            if (potentialExtras.Count > 0) // np. zostaw bufor dla innych zmian
                            {
                                var extra = potentialExtras.First();
                                gene.AssignedEmployees.Add(extra);
                                employeeHours[extra.Id] += 8;
                                dailyAssignments.Add((extra.Id, date));
                                lastShiftMap[extra.Id] = (date, shift);
                            }
                        }

                    }
                }

                population.Add(chrom);
            }

            return population;
        }




        // Wersja diagnostyczna: liczy DOKŁADNIE ten sam fitness co EvaluateFitness, ale przy okazji
        // zbiera czytelne komunikaty o naruszeniach. Deleguje do EvaluateFitness z parametrem `log`,
        // więc nie istnieje druga, równoległa kopia całej logiki kar (która mogłaby rozjechać się
        // z EvaluateFitness przy każdej zmianie reguł).
        private static List<string> EvaluateFitnessWithErrors(
        Chromosome chrom,
        Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
        int workingHours,
        HashSet<(string empId, DateTime date)> vacationDays,
        List<Employee> employees)
        {
            var log = new List<string>();
            double fitness = EvaluateFitness(chrom, shiftRequirements, workingHours, vacationDays, employees, log);

            log.Insert(0, $"Fitness końcowy: {fitness:0.#####}");
            log.Insert(1, "--- Diagnoza ---");

            return log;
        }




        private static double GetDayWeight(DateTime date, DateTime periodEnd)
        {
            double weight = 1.0;

            // 🟠 Weekend ma większą wagę
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                weight += 0.5;

            // 🔴 Końcówka miesiąca = +1.0
            int daysToEnd = (periodEnd - date).Days;
            if (daysToEnd <= 3)
                weight += 1.0;

            return weight;
        }

        private static bool IsEligibleAfterPreviousShift(string empId, DateTime currentDate, ShiftType currentShift, Dictionary<string, (DateTime date, ShiftType shift)> lastShiftMap)
        {
            if (!lastShiftMap.TryGetValue(empId, out var lastShift))
                return true;

            var lastDate = lastShift.date.Date;
            var today = currentDate.Date;

            var dayDiff = (today - lastDate).Days;

            // 👇 Zakładamy że rozpatrujemy kolejne dni – wszystko tego samego dnia już i tak odpada
            if (dayDiff == 1)
            {
                // ⛔ Po nocnej tylko nocna
                if (lastShift.shift == ShiftType.Night && currentShift != ShiftType.Night)
                    return false;

                // ⛔ Po popołudniowej nie można porannej
                if (lastShift.shift == ShiftType.Afternoon && currentShift == ShiftType.Morning)
                    return false;
            }

            return true;
        }

        private static bool IsMaxConsecutiveHoursValid(string empId, DateTime date, ShiftType shift, Dictionary<string, (DateTime date, ShiftType shift)> lastShiftMap)
        {
            if (!lastShiftMap.TryGetValue(empId, out var lastShift))
                return true;

            DateTime prevEnd = GetShiftEnd(lastShift.date, lastShift.shift);
            DateTime currentEnd = GetShiftEnd(date, shift);

            if ((date - lastShift.date).TotalDays > 1)
                return true; // miał dzień przerwy

            double shiftDuration = (currentEnd - GetShiftStart(date, shift)).TotalHours;
            double previousDuration = (prevEnd - GetShiftStart(lastShift.date, lastShift.shift)).TotalHours;

            return (previousDuration + shiftDuration) <= 40;
        }

        public static double EvaluateFitnessWrapper(Chromosome chrom, Dictionary<DateTime, Dictionary<ShiftType, int>> req, int hours, HashSet<(string, DateTime)> vacations, List<Employee> employees)
        {
            // chrom mógł być zbudowany/zmodyfikowany poza GA (np. rekonstrukcja w kontrolerze),
            // więc liczymy świeżo, z pominięciem cache.
            chrom.CachedFitness = null;
            return EvaluateFitness(chrom, req, hours, vacations, employees);
        }

    }
}