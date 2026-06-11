using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Npgsql;
using Taneco.Models;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Taneco.Services;

public class DatabaseService
{
    private string _connectionString;

    public DatabaseService()
    {
        _connectionString = "Host=localhost;Port=5432;Database=kp;Username=postgres;Password=123;";
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static bool IsPasswordValid(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;
        if (password.Length < 8)
            return false;
        string forbiddenChars = @"\*#₽&";
        if (Regex.IsMatch(password, $"[{forbiddenChars}]"))
            return false;
        if (!Regex.IsMatch(password, @"\d"))
            return false;
        if (!Regex.IsMatch(password, @"[A-ZА-Я]"))
            return false;
        if (!Regex.IsMatch(password, @"[a-zа-я]"))
            return false;
        return true;
    }

    public static string GetPasswordErrorMessage()
    {
        return "Пароль должен содержать минимум 8 символов, включая:\n" +
               "• Заглавную букву (A-Z, А-Я)\n" +
               "• Строчную букву (a-z, а-я)\n" +
               "• Цифру (0-9)\n" +
               "Пароль не должен содержать специальные символы";
    }

    public static bool IsHireDateValid(DateTime hireDate)
    {
        return hireDate.Date <= DateTime.Today;
    }

    public static string GetHireDateErrorMessage()
    {
        return "Дата приема не может быть в будущем";
    }

    private async Task<NpgsqlConnection> GetConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<User?> AuthenticateAsync(string login, string password)
    {
        const string query = @"
            SELECT
                s.Код_сотр, s.Фамилия, s.Имя, s.Отчество, s.Контактная_информация,
                a.Логин, d.Наименование, o.Наименование, d.Роль_в_приложении
            FROM Авторизация_сотрудник a
            JOIN Сотрудник s ON a.Код_сотр = s.Код_сотр
            JOIN Должность d ON s.Код_должности = d.Код_должности
            JOIN Отдел o ON s.Код_отдела = o.Код_отдела
            WHERE a.Логин = @login AND a.Пароль = @password AND s.Активен = true";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@login", login);
            cmd.Parameters.AddWithValue("@password", password);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    LastName = reader.GetString(1),
                    FirstName = reader.GetString(2),
                    Patronymic = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Login = reader.GetString(5),
                    Position = reader.GetString(6),
                    Department = reader.GetString(7),
                    Role = reader.GetString(8)
                };
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ObservableCollection<Measurement>> GetMeasurementsByDateAsync(DateTime date)
    {
        var measurements = new ObservableCollection<Measurement>();
        const string query = @"
            SELECT
                z.Код_замера,
                dt.Код_датчика,
                d.Точка_контроля,
                t.Наименование as Трубопровод,
                z.Текущее_значение,
                z.Дата_замера,
                COALESCE(z.Время_замера, '00:00:00'::time) as Время_замера,
                rd.Минимальное_значение,
                rd.Максимальное_значение,
                ei.Наименование as Единица_измерения,
                CASE
                    WHEN z.Текущее_значение > rd.Максимальное_значение THEN 'Критично'
                    WHEN z.Текущее_значение < rd.Минимальное_значение THEN 'Критично'
                    WHEN z.Текущее_значение > rd.Максимальное_значение * 0.9 THEN 'Предупреждение'
                    ELSE 'Норма'
                END as Статус
            FROM Замер z
            JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
            JOIN Датчик d ON dt.Код_датчика = d.Код_датчика
            JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
            JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
            JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
            JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
            JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
            WHERE z.Дата_замера = @date
            ORDER BY z.Время_замера";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@date", date);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                measurements.Add(new Measurement
                {
                    Id = reader.GetInt32(0),
                    SensorId = reader.GetInt32(1),
                    SensorName = reader.GetString(2),
                    PipelineName = reader.GetString(3),
                    Value = reader.GetDecimal(4),
                    Date = reader.GetDateTime(5),
                    Time = reader.GetTimeSpan(6),
                    MinThreshold = reader.GetDecimal(7),
                    MaxThreshold = reader.GetDecimal(8),
                    Unit = reader.GetString(9),
                    Status = reader.GetString(10)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMeasurementsByDateAsync error: {ex.Message}");
        }
        return measurements;
    }

    public async Task<ObservableCollection<Measurement>> GetAllMeasurementsAsync()
    {
        var measurements = new ObservableCollection<Measurement>();
        const string query = @"
            SELECT
                z.Код_замера,
                dt.Код_датчика,
                d.Точка_контроля,
                t.Наименование as Трубопровод,
                z.Текущее_значение,
                z.Дата_замера,
                COALESCE(z.Время_замера, '00:00:00'::time) as Время_замера,
                rd.Минимальное_значение,
                rd.Максимальное_значение,
                ei.Наименование as Единица_измерения,
                CASE
                    WHEN z.Текущее_значение > rd.Максимальное_значение THEN 'Критично'
                    WHEN z.Текущее_значение < rd.Минимальное_значение THEN 'Критично'
                    WHEN z.Текущее_значение > rd.Максимальное_значение * 0.9 THEN 'Предупреждение'
                    ELSE 'Норма'
                END as Статус
            FROM Замер z
            JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
            JOIN Датчик d ON dt.Код_датчика = d.Код_датчика
            JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
            JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
            JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
            JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
            JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
            WHERE z.Дата_замера IS NOT NULL
            ORDER BY z.Дата_замера DESC, z.Время_замера DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                measurements.Add(new Measurement
                {
                    Id = reader.GetInt32(0),
                    SensorId = reader.GetInt32(1),
                    SensorName = reader.GetString(2),
                    PipelineName = reader.GetString(3),
                    Value = reader.GetDecimal(4),
                    Date = reader.GetDateTime(5),
                    Time = reader.GetTimeSpan(6),
                    MinThreshold = reader.GetDecimal(7),
                    MaxThreshold = reader.GetDecimal(8),
                    Unit = reader.GetString(9),
                    Status = reader.GetString(10)
                });
            }
        }
        catch
        {
        }
        return measurements;
    }

    public async Task<ObservableCollection<Pipeline>> GetPipelinesAsync()
    {
        var pipelines = new ObservableCollection<Pipeline>();
        const string query = @"
            SELECT
                t.Код_трубопровода,
                t.Наименование,
                t.Дата_установки,
                m.Наименование as Материал,
                vt.Протяженность,
                vt.Диаметр
            FROM Трубопровод t
            JOIN Вид_трубопровода vt ON t.Код_вида_труб = vt.Код_вида_труб
            JOIN Материал m ON vt.Код_материала = m.Код_материала
            ORDER BY t.Код_трубопровода";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pipelines.Add(new Pipeline
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    InstallationDate = reader.GetDateTime(2),
                    Material = reader.GetString(3),
                    Length = reader.GetDecimal(4),
                    Diameter = reader.GetDecimal(5)
                });
            }
        }
        catch
        {
        }
        return pipelines;
    }

    public async Task<ObservableCollection<Problem>> GetActiveProblemsAsync()
    {
        var problems = new ObservableCollection<Problem>();
        const string query = @"
        SELECT DISTINCT
            up.Код_уведом_проб,
            COALESCE(tp.Наименование, 'Неизвестный тип') as Тип_проблемы,
            COALESCE(up.Описание, 'Нет описания') as Описание,
            up.Дата_уведомления,
            COALESCE(up.Время_уведомления, '00:00:00'::time) as Время_уведомления,
            COALESCE(t.Код_трубопровода, 0) as Код_трубопровода,
            COALESCE(t.Наименование, 'Неизвестный трубопровод') as Трубопровод,
            COALESCE(z.Текущее_значение, 0) as Текущее_значение,
            COALESCE(rd.Максимальное_значение, 0) as Максимальное_значение,
            COALESCE(tp.Категория_риска, 'Средний') as Категория_риска,
            CASE
                WHEN p.Код_проверки IS NULL THEN 'Новая'
                WHEN st.Код_статуса_проверки = 6 THEN 'Завершена'
                WHEN st.Код_статуса_проверки = 7 THEN 'Отложена'
                WHEN st.Код_статуса_проверки = 8 THEN 'Отменена'
                WHEN st.Код_статуса_проверки = 4 THEN 'Ожидает подтверждения'
                WHEN st.Код_статуса_проверки = 2 THEN 'В процессе выполнения'
                ELSE COALESCE(st.Наименование, 'В работе')
            END as Статус,
            up.Дата_уведомления as SortDate,
            up.Время_уведомления as SortTime
        FROM Уведомление_проблемы up
        LEFT JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
        LEFT JOIN Замер z ON up.Код_замера = z.Код_замера
        LEFT JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
        LEFT JOIN Датчик d ON dt.Код_датчика = d.Код_датчика
        LEFT JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
        LEFT JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
        LEFT JOIN Проверка p ON up.Код_уведом_проб = p.Код_уведом_проб
        LEFT JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
        WHERE up.Дата_уведомления IS NOT NULL
        AND (p.Код_проверки IS NULL OR st.Код_статуса_проверки NOT IN (6, 7, 8))
        ORDER BY SortDate DESC, SortTime DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var problem = new Problem
                {
                    Id = reader.GetInt32(0),
                    Type = reader.GetString(1),
                    Description = reader.GetString(2),
                    NotificationDate = reader.GetDateTime(3),
                    NotificationTime = reader.GetTimeSpan(4),
                    PipelineId = reader.GetInt32(5),
                    PipelineName = reader.GetString(6),
                    MeasuredValue = reader.GetDecimal(7),
                    ThresholdValue = reader.GetDecimal(8),
                    RiskCategory = reader.GetString(9),
                    Status = reader.GetString(10)
                };
                problems.Add(problem);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetActiveProblemsAsync error: {ex.Message}");
        }
        return problems;
    }

    // ИСПРАВЛЕННЫЙ метод для получения активных проблем за конкретную дату
    public async Task<ObservableCollection<Problem>> GetActiveProblemsByDateAsync(DateTime date)
    {
        var problems = new ObservableCollection<Problem>();
        const string query = @"
        SELECT DISTINCT
            up.Код_уведом_проб,
            COALESCE(tp.Наименование, 'Неизвестный тип') as Тип_проблемы,
            COALESCE(up.Описание, 'Нет описания') as Описание,
            up.Дата_уведомления,
            COALESCE(up.Время_уведомления, '00:00:00'::time) as Время_уведомления,
            COALESCE(t.Код_трубопровода, 0) as Код_трубопровода,
            COALESCE(t.Наименование, 'Неизвестный трубопровод') as Трубопровод,
            COALESCE(z.Текущее_значение, 0) as Текущее_значение,
            COALESCE(rd.Максимальное_значение, 0) as Максимальное_значение,
            COALESCE(tp.Категория_риска, 'Средний') as Категория_риска,
            CASE
                WHEN p.Код_проверки IS NULL THEN 'Новая'
                WHEN st.Код_статуса_проверки = 6 THEN 'Завершена'
                WHEN st.Код_статуса_проверки = 7 THEN 'Отложена'
                WHEN st.Код_статуса_проверки = 8 THEN 'Отменена'
                WHEN st.Код_статуса_проверки = 4 THEN 'Ожидает подтверждения'
                WHEN st.Код_статуса_проверки = 2 THEN 'В процессе выполнения'
                ELSE COALESCE(st.Наименование, 'В работе')
            END as Статус,
            up.Дата_уведомления as SortDate,
            up.Время_уведомления as SortTime
        FROM Уведомление_проблемы up
        LEFT JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
        LEFT JOIN Замер z ON up.Код_замера = z.Код_замера
        LEFT JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
        LEFT JOIN Датчик d ON dt.Код_датчика = d.Код_датчика
        LEFT JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
        LEFT JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
        LEFT JOIN Проверка p ON up.Код_уведом_проб = p.Код_уведом_проб
        LEFT JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
        WHERE CAST(up.Дата_уведомления AS DATE) = CAST(@date AS DATE)
        AND (p.Код_проверки IS NULL OR st.Код_статуса_проверки NOT IN (6, 7, 8))
        ORDER BY SortDate DESC, SortTime DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@date", date);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var problem = new Problem
                {
                    Id = reader.GetInt32(0),
                    Type = reader.GetString(1),
                    Description = reader.GetString(2),
                    NotificationDate = reader.GetDateTime(3),
                    NotificationTime = reader.GetTimeSpan(4),
                    PipelineId = reader.GetInt32(5),
                    PipelineName = reader.GetString(6),
                    MeasuredValue = reader.GetDecimal(7),
                    ThresholdValue = reader.GetDecimal(8),
                    RiskCategory = reader.GetString(9),
                    Status = reader.GetString(10)
                };
                problems.Add(problem);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetActiveProblemsByDateAsync error: {ex.Message}");
        }
        return problems;
    }

    public async Task<ObservableCollection<ProblemHistoryEvent>> GetProblemHistoryAsync(int problemId)
    {
        var history = new ObservableCollection<ProblemHistoryEvent>();

        const string query = @"
            SELECT 
                'Проблема обнаружена' as Событие,
                up.Дата_уведомления as Дата,
                up.Время_уведомления as Время,
                up.Описание as Детали,
                1 as Порядок
            FROM Уведомление_проблемы up
            WHERE up.Код_уведом_проб = @problemId
            
            UNION ALL
            
            SELECT 
                'Назначена проверка' as Событие,
                p.Дата_начала as Дата,
                NULL as Время,
                CONCAT('Проверка: ', COALESCE(p.Описание, 'Нет описания'), 
                       '. Ответственный: ', COALESCE(s.Фамилия, ''), ' ', COALESCE(s.Имя, '')) as Детали,
                2 as Порядок
            FROM Проверка p
            LEFT JOIN Сотрудник s ON p.Код_сотр = s.Код_сотр
            WHERE p.Код_уведом_проб = @problemId
            
            UNION ALL
            
            SELECT 
                CONCAT('Статус проверки: ', st.Наименование) as Событие,
                COALESCE(p.Дата_окончания, p.Дата_начала) as Дата,
                NULL as Время,
                COALESCE(p.Описание, 'Нет дополнительной информации') as Детали,
                3 as Порядок
            FROM Проверка p
            JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
            WHERE p.Код_уведом_проб = @problemId
            
            UNION ALL
            
            SELECT 
                CONCAT('Ремонт: ', sr.Наименование) as Событие,
                r.Дата_начала as Дата,
                NULL as Время,
                CONCAT('Бригада: ', b.Наименование, '. Бюджет: ', r.Бюджет, ' руб.') as Детали,
                4 as Порядок
            FROM Ремонт r
            JOIN Проверка p ON r.Код_проверки = p.Код_проверки
            JOIN Статус_ремонта sr ON r.Код_статуса_ремонта = sr.Код_статуса_ремонта
            JOIN Бригада_сотрудник bs ON r.Код_бриг_сотр = bs.Код_бриг_сотр
            JOIN Бригада b ON bs.Код_бриг = b.Код_бриг
            WHERE p.Код_уведом_проб = @problemId
            
            UNION ALL
            
            SELECT 
                CASE 
                    WHEN st.Код_статуса_проверки = 6 THEN 'Ремонт завершен'
                    WHEN st.Код_статуса_проверки = 7 THEN 'Проверка отложена'
                    WHEN st.Код_статуса_проверки = 8 THEN 'Проверка отменена'
                    ELSE 'Проблема закрыта'
                END as Событие,
                COALESCE(p.Дата_окончания, CURRENT_DATE) as Дата,
                NULL as Время,
                'Проблема закрыта' as Детали,
                5 as Порядок
            FROM Проверка p
            JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
            WHERE p.Код_уведом_проб = @problemId
            AND st.Код_статуса_проверки IN (6, 7, 8)
            
            ORDER BY Дата ASC, Порядок ASC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@problemId", problemId);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var eventDate = reader.GetDateTime(1);
                var eventTime = reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetTimeSpan(2);

                history.Add(new ProblemHistoryEvent
                {
                    Id = history.Count + 1,
                    EventName = reader.GetString(0),
                    EventDate = eventDate,
                    EventTime = eventTime,
                    Details = reader.GetString(3),
                    EventType = GetEventType(reader.GetString(0))
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetProblemHistoryAsync error: {ex.Message}");
        }

        return history;
    }

    private string GetEventType(string eventName)
    {
        if (eventName.Contains("обнаружена")) return "detection";
        if (eventName.Contains("проверк")) return "inspection";
        if (eventName.Contains("Ремонт")) return "repair";
        if (eventName.Contains("завершен") || eventName.Contains("отмен") || eventName.Contains("отлож")) return "completion";
        return "default";
    }

    public async Task<ObservableCollection<Problem>> GetAllProblemsAsync()
    {
        var problems = new ObservableCollection<Problem>();
        const string query = @"
            SELECT
                up.Код_уведом_проб,
                tp.Наименование,
                up.Описание,
                up.Дата_уведомления,
                up.Время_уведомления,
                t.Код_трубопровода,
                t.Наименование,
                COALESCE(st.Наименование, 'Новая') as Статус,
                z.Текущее_значение,
                rd.Максимальное_значение,
                tp.Категория_риска
            FROM Уведомление_проблемы up
            JOIN Замер z ON up.Код_замера = z.Код_замера
            JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
            JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
            JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
            JOIN Датчик d ON dt.Код_датчика = d.Код_датчика
            JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
            JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
            LEFT JOIN Проверка p ON up.Код_уведом_проб = p.Код_уведом_проб
            LEFT JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
            ORDER BY up.Дата_уведомления DESC, up.Время_уведомления DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                problems.Add(new Problem
                {
                    Id = reader.GetInt32(0),
                    Type = reader.GetString(1),
                    Description = reader.GetString(2),
                    NotificationDate = reader.GetDateTime(3),
                    NotificationTime = reader.GetTimeSpan(4),
                    PipelineId = reader.GetInt32(5),
                    PipelineName = reader.GetString(6),
                    Status = reader.GetString(7),
                    MeasuredValue = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                    ThresholdValue = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                    RiskCategory = reader.IsDBNull(10) ? "Средний" : reader.GetString(10)
                });
            }
        }
        catch
        {
        }
        return problems;
    }

    public async Task<ObservableCollection<Inspection>> GetInspectionsForInspectorAsync(int inspectorId)
    {
        var inspections = new ObservableCollection<Inspection>();
        const string query = @"
            SELECT
                p.Код_проверки,
                p.Код_уведом_проб,
                p.Код_сотр,
                CONCAT(s.Фамилия, ' ', s.Имя, ' ', COALESCE(s.Отчество, '')) as Сотрудник,
                p.Дата_начала,
                p.Дата_окончания,
                st.Наименование as Статус,
                p.Описание,
                up.Описание as Проблема_описание,
                tp.Наименование as Тип_проблемы
            FROM Проверка p
            JOIN Сотрудник s ON p.Код_сотр = s.Код_сотр
            JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
            JOIN Уведомление_проблемы up ON p.Код_уведом_проб = up.Код_уведом_проб
            JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
            WHERE p.Код_сотр = @inspectorId
            ORDER BY p.Дата_начала DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@inspectorId", inspectorId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                inspections.Add(new Inspection
                {
                    Id = reader.GetInt32(0),
                    ProblemId = reader.GetInt32(1),
                    EmployeeId = reader.GetInt32(2),
                    EmployeeName = reader.GetString(3),
                    StartDate = reader.GetDateTime(4),
                    EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    Description = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    ProblemDescription = reader.GetString(8),
                    ProblemType = reader.GetString(9)
                });
            }
        }
        catch
        {
        }
        return inspections;
    }

    public async Task<ObservableCollection<Inspection>> GetAllInspectionsAsync()
    {
        var inspections = new ObservableCollection<Inspection>();
        const string query = @"
            SELECT
                p.Код_проверки,
                p.Код_уведом_проб,
                p.Код_сотр,
                CONCAT(s.Фамилия, ' ', s.Имя, ' ', COALESCE(s.Отчество, '')) as Сотрудник,
                p.Дата_начала,
                p.Дата_окончания,
                st.Наименование as Статус,
                p.Описание,
                up.Описание as Проблема_описание,
                tp.Наименование as Тип_проблемы
            FROM Проверка p
            JOIN Сотрудник s ON p.Код_сотр = s.Код_сотр
            JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
            JOIN Уведомление_проблемы up ON p.Код_уведом_проб = up.Код_уведом_проб
            JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
            ORDER BY p.Дата_начала DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                inspections.Add(new Inspection
                {
                    Id = reader.GetInt32(0),
                    ProblemId = reader.GetInt32(1),
                    EmployeeId = reader.GetInt32(2),
                    EmployeeName = reader.GetString(3),
                    StartDate = reader.GetDateTime(4),
                    EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    Description = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    ProblemDescription = reader.GetString(8),
                    ProblemType = reader.GetString(9)
                });
            }
        }
        catch
        {
        }
        return inspections;
    }

    public async Task<ObservableCollection<Inspection>> GetAllInspectionsForReportAsync()
    {
        var inspections = new ObservableCollection<Inspection>();
        const string query = @"
            SELECT
                p.Код_проверки,
                p.Код_уведом_проб,
                p.Код_сотр,
                CONCAT(COALESCE(s.Фамилия, ''), ' ', COALESCE(s.Имя, ''), ' ', COALESCE(s.Отчество, '')) as Сотрудник,
                p.Дата_начала,
                p.Дата_окончания,
                COALESCE(st.Наименование, 'Новая') as Статус,
                COALESCE(p.Описание, '') as Описание,
                COALESCE(up.Описание, '') as Проблема_описание,
                COALESCE(tp.Наименование, 'Неизвестно') as Тип_проблемы
            FROM Проверка p
            LEFT JOIN Сотрудник s ON p.Код_сотр = s.Код_сотр
            LEFT JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
            LEFT JOIN Уведомление_проблемы up ON p.Код_уведом_проб = up.Код_уведом_проб
            LEFT JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
            ORDER BY p.Дата_начала DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var inspection = new Inspection
                {
                    Id = reader.GetInt32(0),
                    ProblemId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    EmployeeId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    EmployeeName = reader.IsDBNull(3) ? "Не назначен" : reader.GetString(3).Trim(),
                    StartDate = reader.GetDateTime(4),
                    EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    Description = reader.GetString(7),
                    ProblemDescription = reader.GetString(8),
                    ProblemType = reader.GetString(9)
                };
                inspections.Add(inspection);
            }

            Console.WriteLine($"GetAllInspectionsForReportAsync: загружено {inspections.Count} проверок");

            for (int i = 0; i < Math.Min(5, inspections.Count); i++)
            {
                Console.WriteLine($"  Проверка {i + 1}: ID={inspections[i].Id}, Дата={inspections[i].StartDate:yyyy-MM-dd}, Статус={inspections[i].Status}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllInspectionsForReportAsync error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        return inspections;
    }

    public async Task<bool> GenerateReportAsync(DateTime startDate, DateTime endDate, string savePath)
    {
        try
        {
            Console.WriteLine($"GenerateReportAsync started: startDate={startDate:yyyy-MM-dd}, endDate={endDate:yyyy-MM-dd}, savePath={savePath}");

            if (string.IsNullOrWhiteSpace(savePath))
            {
                Console.WriteLine("GenerateReportAsync error: savePath is null or empty");
                return false;
            }

            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var inspections = await GetAllInspectionsForReportAsync();

            Console.WriteLine($"Всего проверок в БД: {inspections.Count}");

            var filteredInspections = inspections
                .Where(i => i.StartDate.Date >= startDate.Date && i.StartDate.Date <= endDate.Date)
                .ToList();

            Console.WriteLine($"Отфильтровано проверок за период: {filteredInspections.Count}");

            if (filteredInspections.Count == 0)
            {
                Console.WriteLine("GenerateReportAsync: No inspections found for the date range");
                return false;
            }

            await Task.Run(() => GeneratePdfWithQuestPDF(filteredInspections, startDate, endDate, savePath));

            Console.WriteLine("GenerateReportAsync: PDF successfully created");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GenerateReportAsync error: {ex.Message}");
            Console.WriteLine($"GenerateReportAsync stack trace: {ex.StackTrace}");
            return false;
        }
    }

    private void GeneratePdfWithQuestPDF(List<Inspection> inspections, DateTime startDate, DateTime endDate, string filePath)
    {
        try
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .AlignCenter()
                        .Column(col =>
                        {
                            col.Spacing(5);
                            col.Item().Text("ОТЧЕТ ПО ПРОВЕРКАМ").Bold().FontSize(18);
                            col.Item().Text($"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}").FontSize(12);
                            col.Item().Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}").FontSize(11);
                            col.Item().PaddingTop(10).LineHorizontal(1);
                        });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Column(statsCol =>
                        {
                            statsCol.Spacing(5);
                            statsCol.Item().Text("СТАТИСТИКА ПО СТАТУСАМ:").Bold().FontSize(13);

                            int completed = inspections.Count(i => i.Status == "Завершена");
                            int inProgress = inspections.Count(i => i.Status == "В процессе выполнения");
                            int planned = inspections.Count(i => i.Status == "Запланирована");
                            int cancelled = inspections.Count(i => i.Status == "Отменена");
                            int delayed = inspections.Count(i => i.Status == "Отложена");
                            int waiting = inspections.Count(i => i.Status == "Ожидает подтверждения");
                            int analyzing = inspections.Count(i => i.Status == "На анализе данных");
                            int urgent = inspections.Count(i => i.Status == "Требует срочного вмешательства");
                            int pending = inspections.Count(i => i.Status == "На согласовании");

                            statsCol.Item().Text(text =>
                            {
                                text.Span($"• Завершено: {completed}\n");
                                text.Span($"• В процессе: {inProgress}\n");
                                text.Span($"• Запланировано: {planned}\n");
                                text.Span($"• Отменено: {cancelled}\n");
                                text.Span($"• Отложено: {delayed}\n");
                                text.Span($"• Ожидает подтверждения: {waiting}\n");
                                text.Span($"• На анализе данных: {analyzing}\n");
                                text.Span($"• Требует срочного вмешательства: {urgent}\n");
                                text.Span($"• На согласовании: {pending}");
                            });
                        });

                        col.Item().LineHorizontal(1);

                        col.Item().Text($"ПОДРОБНЫЙ СПИСОК ПРОВЕРОК (всего: {inspections.Count})").Bold().FontSize(13);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("№").Bold().AlignCenter();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Тип проблемы").Bold().AlignCenter();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Описание").Bold().AlignCenter();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Инспектор").Bold().AlignCenter();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Дата начала").Bold().AlignCenter();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Дата окончания").Bold().AlignCenter();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Статус").Bold().AlignCenter();
                            });

                            int num = 1;
                            foreach (var inv in inspections)
                            {
                                var rowColor = num % 2 == 0 ? Colors.Grey.Lighten3 : Colors.White;

                                table.Cell().Background(rowColor).Padding(4).Text(num.ToString()).AlignCenter();
                                table.Cell().Background(rowColor).Padding(4).Text(TruncateText(inv.ProblemType, 25));
                                table.Cell().Background(rowColor).Padding(4).Text(TruncateText(inv.ProblemDescription, 40));
                                table.Cell().Background(rowColor).Padding(4).Text(TruncateText(inv.EmployeeName, 20));
                                table.Cell().Background(rowColor).Padding(4).Text(inv.StartDate.ToString("dd.MM.yyyy")).AlignCenter();
                                table.Cell().Background(rowColor).Padding(4).Text(inv.EndDate?.ToString("dd.MM.yyyy") ?? "—").AlignCenter();

                                table.Cell().Background(rowColor).Padding(4).Text(inv.Status)
                                    .FontColor(GetStatusColor(inv.Status))
                                    .SemiBold();

                                num++;
                            }
                        });
                    });

                    page.Footer()
                        .AlignCenter()
                        .Column(col =>
                        {
                            col.Spacing(5);
                            col.Item().PaddingTop(10).LineHorizontal(1);
                            col.Item().Text("КОНЕЦ ОТЧЕТА").Bold().FontSize(10);
                            col.Item().Text($"Сформировано автоматически системой мониторинга {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8);
                        });
                });
            });

            document.GeneratePdf(filePath);
            Console.WriteLine($"PDF успешно создан: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GeneratePdfWithQuestPDF error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private string GetStatusColor(string status)
    {
        switch (status)
        {
            case "Завершена":
                return Colors.Green.Darken2;
            case "Отменена":
                return Colors.Red.Medium;
            case "Отложена":
                return Colors.Orange.Medium;
            case "В процессе выполнения":
                return Colors.Blue.Medium;
            case "Требует срочного вмешательства":
                return Colors.Red.Darken2;
            default:
                return Colors.Black;
        }
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "—";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
    }

    public async Task<bool> CreateInspectionAsync(int problemId, int employeeId, string description, string urgency)
    {
        const string query = @"
            INSERT INTO Проверка (Код_проверки, Код_сотр, Код_уведом_проб, Дата_начала, Код_статуса_проверки, Описание)
            VALUES ((SELECT COALESCE(MAX(Код_проверки), 0) + 1 FROM Проверка),
                    @employeeId, @problemId, CURRENT_DATE, 1, @description)";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            cmd.Parameters.AddWithValue("@problemId", problemId);
            cmd.Parameters.AddWithValue("@description", $"{description} (Срочность: {urgency})");
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateInspectionStatusAsync(int inspectionId, string status, string? reason = null)
    {
        int statusId = status switch
        {
            "В процессе выполнения" => 2,
            "Требует дополнительной диагностики" => 3,
            "Ожидает подтверждения" => 4,
            "На анализе данных" => 5,
            "Завершена" => 6,
            "Отложена" => 7,
            "Отменена" => 8,
            "Требует срочного вмешательства" => 9,
            "На согласовании" => 10,
            _ => 1
        };

        string updateQuery = @"
            UPDATE Проверка
            SET Код_статуса_проверки = @statusId,
                Дата_окончания = CASE WHEN @statusId IN (6, 7, 8) THEN CURRENT_DATE ELSE NULL END,
                Описание = CASE WHEN @reason IS NOT NULL THEN CONCAT(Описание, ' | Причина: ', @reason) ELSE Описание END
            WHERE Код_проверки = @inspectionId";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@statusId", statusId);
            cmd.Parameters.AddWithValue("@inspectionId", inspectionId);
            cmd.Parameters.AddWithValue("@reason", reason ?? (object)DBNull.Value);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreateScheduledInspectionAsync(int employeeId, int pipelineId, string inspectionType, DateTime scheduledDate, string description)
    {
        const string query = @"
            INSERT INTO Проверка (Код_проверки, Код_сотр, Код_уведом_проб, Дата_начала, Код_статуса_проверки, Описание)
            VALUES ((SELECT COALESCE(MAX(Код_проверки), 0) + 1 FROM Проверка),
                    @employeeId, 
                    (SELECT Код_уведом_проб FROM Уведомление_проблемы LIMIT 1),
                    @scheduledDate, 1, @description)";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            cmd.Parameters.AddWithValue("@scheduledDate", scheduledDate);
            cmd.Parameters.AddWithValue("@description", $"[Плановая проверка] Тип: {inspectionType}. {description}");
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch
        {
            return false;
        }
    }

    // ИСПРАВЛЕННЫЙ метод для обновления статуса проблемы
    public async Task<bool> UpdateProblemStatusAsync(int problemId, string status)
    {
        const string findInspectionQuery = @"
        SELECT Код_проверки FROM Проверка 
        WHERE Код_уведом_проб = @problemId 
        ORDER BY Код_проверки DESC LIMIT 1";

        try
        {
            await using var conn = await GetConnectionAsync();

            int? inspectionId = null;
            await using (var findCmd = new NpgsqlCommand(findInspectionQuery, conn))
            {
                findCmd.Parameters.AddWithValue("@problemId", problemId);
                var result = await findCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    inspectionId = Convert.ToInt32(result);
                }
            }

            if (!inspectionId.HasValue)
            {
                Console.WriteLine($"No inspection found for problem {problemId}");
                return false;
            }

            int statusId = status switch
            {
                "Ложные показания" => 6,
                "Завершена" => 6,
                "Требуется ремонт" => 9,
                "В процессе ремонта" => 9,
                "Завершен" => 6,
                "Ожидает подтверждения" => 4,
                "В процессе выполнения" => 2,
                _ => 4
            };

            const string updateQuery = @"
            UPDATE Проверка
            SET Код_статуса_проверки = @statusId,
                Дата_окончания = CASE WHEN @statusId IN (6, 7, 8) THEN CURRENT_DATE ELSE NULL END
            WHERE Код_проверки = @inspectionId";

            await using var updateCmd = new NpgsqlCommand(updateQuery, conn);
            updateCmd.Parameters.AddWithValue("@statusId", statusId);
            updateCmd.Parameters.AddWithValue("@inspectionId", inspectionId.Value);

            int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

            Console.WriteLine($"UpdateProblemStatusAsync: problemId={problemId}, status={status}, statusId={statusId}, rowsAffected={rowsAffected}");

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateProblemStatusAsync error: {ex.Message}");
            return false;
        }
    }

    // НОВЫЙ метод для обновления статуса проблемы со срочностью
    public async Task<bool> UpdateProblemStatusWithUrgencyAsync(int problemId, string status, string urgency)
    {
        try
        {
            await using var conn = await GetConnectionAsync();

            const string findInspectionQuery = @"
                SELECT Код_проверки FROM Проверка 
                WHERE Код_уведом_проб = @problemId 
                ORDER BY Код_проверки DESC LIMIT 1";

            int? inspectionId = null;
            await using (var findCmd = new NpgsqlCommand(findInspectionQuery, conn))
            {
                findCmd.Parameters.AddWithValue("@problemId", problemId);
                var result = await findCmd.ExecuteScalarAsync();
                if (result != null)
                {
                    inspectionId = Convert.ToInt32(result);
                }
            }

            if (inspectionId.HasValue)
            {
                int statusId = status switch
                {
                    "Ожидает подтверждения" => 4,
                    "В процессе выполнения" => 2,
                    "Ложные показания" => 6,
                    "Завершена" => 6,
                    "Требуется ремонт" => 9,
                    "В процессе ремонта" => 9,
                    "Завершен" => 6,
                    _ => 1
                };

                const string updateQuery = @"
                    UPDATE Проверка
                    SET Код_статуса_проверки = @statusId,
                        Дата_окончания = CASE WHEN @statusId IN (6, 7, 8) THEN CURRENT_DATE ELSE NULL END,
                        Описание = CONCAT(COALESCE(Описание, ''), ' | Срочность: ', @urgency)
                    WHERE Код_проверки = @inspectionId";

                await using var updateCmd = new NpgsqlCommand(updateQuery, conn);
                updateCmd.Parameters.AddWithValue("@statusId", statusId);
                updateCmd.Parameters.AddWithValue("@inspectionId", inspectionId.Value);
                updateCmd.Parameters.AddWithValue("@urgency", urgency);
                await updateCmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateProblemStatusWithUrgencyAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<ObservableCollection<Sensor>> GetSensorsAsync()
    {
        var sensors = new ObservableCollection<Sensor>();
        const string query = @"
            SELECT
                d.Код_датчика,
                m.Наименование as Модель,
                p.Наименование as Производитель,
                p.Страна,
                td.Наименование as Тип,
                rd.Минимальное_значение,
                rd.Максимальное_значение,
                dim.Наименование as Измерение,
                ei.Наименование as Единица,
                d.Точка_контроля,
                COALESCE(t.Наименование, 'Не установлен') as Трубопровод,
                COALESCE(dt.Местоположение, '') as Местоположение,
                d.Год_выпуска,
                d.Дата_последней_поверки
            FROM Датчик d
            JOIN Модель m ON d.Код_модели = m.Код_модели
            JOIN Производитель p ON m.Код_производ = p.Код_производ
            JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
            JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
            JOIN Тип_датчика td ON od.Код_типа_дат = td.Код_типа_дат
            JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
            JOIN Измерение_датчика dim ON oi.Код_измер_дат = dim.Код_измер_дат
            JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
            LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
            LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
            ORDER BY d.Код_датчика";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sensors.Add(new Sensor
                {
                    Id = reader.GetInt32(0),
                    Model = reader.GetString(1),
                    Manufacturer = reader.GetString(2),
                    ManufacturerCountry = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Type = reader.GetString(4),
                    MinValue = reader.GetDecimal(5),
                    MaxValue = reader.GetDecimal(6),
                    MeasurementType = reader.GetString(7),
                    Unit = reader.GetString(8),
                    ControlPoint = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    PipelineName = reader.GetString(10),
                    Location = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    ProductionYear = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    LastCalibration = reader.IsDBNull(13) ? null : reader.GetDateTime(13)
                });
            }
        }
        catch
        {
        }
        return sensors;
    }

    public async Task<bool> UpdateSensorThresholdsAsync(int sensorId, decimal minValue, decimal maxValue)
    {
        const string query = @"
            UPDATE Работа_датчика
            SET Минимальное_значение = @minValue, Максимальное_значение = @maxValue
            WHERE Код_раб_дат = (
                SELECT od.Код_раб_дат FROM Особенности_датчика od
                JOIN Датчик d ON d.Код_особ_дат = od.Код_особ_дат
                WHERE d.Код_датчика = @sensorId
            )";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@sensorId", sensorId);
            cmd.Parameters.AddWithValue("@minValue", minValue);
            cmd.Parameters.AddWithValue("@maxValue", maxValue);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ObservableCollection<Repair>> GetRepairsAsync()
    {
        var repairs = new ObservableCollection<Repair>();
        const string query = @"
            SELECT
                r.Код_ремонта,
                up.Код_уведом_проб,
                up.Описание,
                b.Код_бриг,
                b.Наименование,
                r.Дата_начала,
                r.Дата_окончания,
                r.Бюджет,
                sr.Наименование as Статус
            FROM Ремонт r
            JOIN Проверка p ON r.Код_проверки = p.Код_проверки
            JOIN Уведомление_проблемы up ON p.Код_уведом_проб = up.Код_уведом_проб
            JOIN Бригада_сотрудник bs ON r.Код_бриг_сотр = bs.Код_бриг_сотр
            JOIN Бригада b ON bs.Код_бриг = b.Код_бриг
            JOIN Статус_ремонта sr ON r.Код_статуса_ремонта = sr.Код_статуса_ремонта
            ORDER BY r.Дата_начала DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                repairs.Add(new Repair
                {
                    Id = reader.GetInt32(0),
                    ProblemId = reader.GetInt32(1),
                    ProblemDescription = reader.GetString(2),
                    TeamId = reader.GetInt32(3),
                    TeamName = reader.GetString(4),
                    StartDate = reader.GetDateTime(5),
                    EndDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Budget = reader.GetDecimal(7),
                    Status = reader.GetString(8)
                });
            }
        }
        catch
        {
        }
        return repairs;
    }

    public async Task<ObservableCollection<Repair>> GetRepairsByManagerAsync(int managerId)
    {
        var repairs = new ObservableCollection<Repair>();
        const string query = @"
            SELECT
                r.Код_ремонта,
                up.Код_уведом_проб,
                up.Описание,
                b.Код_бриг,
                b.Наименование,
                r.Дата_начала,
                r.Дата_окончания,
                r.Бюджет,
                sr.Наименование as Статус
            FROM Ремонт r
            JOIN Проверка p ON r.Код_проверки = p.Код_проверки
            JOIN Уведомление_проблемы up ON p.Код_уведом_проб = up.Код_уведом_проб
            JOIN Бригада_сотрудник bs ON r.Код_бриг_сотр = bs.Код_бриг_сотр
            JOIN Бригада b ON bs.Код_бриг = b.Код_бриг
            JOIN Статус_ремонта sr ON r.Код_статуса_ремонта = sr.Код_статуса_ремонта
            WHERE bs.Код_сотр = @managerId
            ORDER BY r.Дата_начала DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@managerId", managerId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                repairs.Add(new Repair
                {
                    Id = reader.GetInt32(0),
                    ProblemId = reader.GetInt32(1),
                    ProblemDescription = reader.GetString(2),
                    TeamId = reader.GetInt32(3),
                    TeamName = reader.GetString(4),
                    StartDate = reader.GetDateTime(5),
                    EndDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Budget = reader.GetDecimal(7),
                    Status = reader.GetString(8)
                });
            }
        }
        catch
        {
        }
        return repairs;
    }

    public async Task<bool> UpdateRepairBudgetAsync(int repairId, decimal budget)
    {
        const string query = "UPDATE Ремонт SET Бюджет = @budget WHERE Код_ремонта = @repairId";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@repairId", repairId);
            cmd.Parameters.AddWithValue("@budget", budget);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AssignRepairTeamAsync(int repairId, int teamId)
    {
        const string query = @"
            UPDATE Ремонт
            SET Код_бриг_сотр = (
                SELECT Код_бриг_сотр FROM Бригада_сотрудник WHERE Код_бриг = @teamId LIMIT 1
            )
            WHERE Код_ремонта = @repairId";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@repairId", repairId);
            cmd.Parameters.AddWithValue("@teamId", teamId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateRepairStatusAsync(int repairId, string status)
    {
        int statusId = status switch
        {
            "Аварийная остановка" => 1,
            "Подготовка к ремонту" => 2,
            "В процессе ремонта" => 3,
            "Испытания после ремонта" => 4,
            "Готов к запуску" => 5,
            "Завершен" => 6,
            "Ожидает поставки материалов" => 7,
            "Требуется проектная документация" => 8,
            "На согласовании метода ремонта" => 9,
            "Консервация оборудования" => 10,
            _ => 3
        };

        const string query = @"
            CALL update_repair_status(@repairId, @statusId, NULL)";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@repairId", repairId);
            cmd.Parameters.AddWithValue("@statusId", statusId);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ObservableCollection<Employee>> GetEmployeesAsync()
    {
        var employees = new ObservableCollection<Employee>();
        const string query = @"
            SELECT
                s.Код_сотр, s.Фамилия, s.Имя, s.Отчество, s.Контактная_информация,
                o.Наименование as Отдел, d.Наименование as Должность,
                d.Роль_в_приложении, a.Логин, s.Дата_приема, s.Активен
            FROM Сотрудник s
            JOIN Отдел o ON s.Код_отдела = o.Код_отдела
            JOIN Должность d ON s.Код_должности = d.Код_должности
            LEFT JOIN Авторизация_сотрудник a ON s.Код_сотр = a.Код_сотр
            ORDER BY s.Фамилия";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                employees.Add(new Employee
                {
                    Id = reader.GetInt32(0),
                    LastName = reader.GetString(1),
                    FirstName = reader.GetString(2),
                    Patronymic = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Department = reader.GetString(5),
                    Position = reader.GetString(6),
                    Role = reader.GetString(7),
                    Login = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    HireDate = reader.GetDateTime(9),
                    IsActive = reader.GetBoolean(10)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetEmployeesAsync error: {ex.Message}");
        }
        return employees;
    }

    public async Task<ObservableCollection<Employee>> GetEmployeesByRoleAsync(string role)
    {
        var employees = new ObservableCollection<Employee>();
        const string query = @"
            SELECT
                s.Код_сотр, s.Фамилия, s.Имя, s.Отчество, s.Контактная_информация,
                o.Наименование as Отдел, d.Наименование as Должность,
                d.Роль_в_приложении, a.Логин, s.Дата_приема, s.Активен
            FROM Сотрудник s
            JOIN Отдел o ON s.Код_отдела = o.Код_отдела
            JOIN Должность d ON s.Код_должности = d.Код_должности
            LEFT JOIN Авторизация_сотрудник a ON s.Код_сотр = a.Код_сотр
            WHERE s.Активен = true AND d.Роль_в_приложении = @role
            ORDER BY s.Фамилия";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@role", role);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                employees.Add(new Employee
                {
                    Id = reader.GetInt32(0),
                    LastName = reader.GetString(1),
                    FirstName = reader.GetString(2),
                    Patronymic = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Department = reader.GetString(5),
                    Position = reader.GetString(6),
                    Role = reader.GetString(7),
                    Login = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    HireDate = reader.GetDateTime(9),
                    IsActive = reader.GetBoolean(10)
                });
            }
        }
        catch
        {
        }
        return employees;
    }

    public async Task<bool> AddEmployeeAsync(string lastName, string firstName, string patronymic, string phone,
        string department, string position, string role, string login, string password, DateTime hireDate)
    {
        try
        {
            await using var conn = await GetConnectionAsync();

            int departmentId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_отдела FROM Отдел WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", department);
                var result = await cmd.ExecuteScalarAsync();
                departmentId = result != null ? (int)result : 1;
            }

            int positionId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_должности FROM Должность WHERE Наименование = @name AND Роль_в_приложении = @role", conn))
            {
                cmd.Parameters.AddWithValue("@name", position);
                cmd.Parameters.AddWithValue("@role", role);
                var result = await cmd.ExecuteScalarAsync();
                positionId = result != null ? (int)result : 1;
            }

            int nextEmployeeId;
            await using (var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(Код_сотр), 0) + 1 FROM Сотрудник", conn))
            {
                nextEmployeeId = (int)(await cmd.ExecuteScalarAsync() ?? 1);
            }

            const string insertEmployee = @"
                INSERT INTO Сотрудник (Код_сотр, Фамилия, Имя, Отчество, Контактная_информация, Код_отдела, Код_должности, Дата_приема, Активен)
                VALUES (@id, @lastName, @firstName, @patronymic, @phone, @departmentId, @positionId, @hireDate, true)";

            await using (var cmd = new NpgsqlCommand(insertEmployee, conn))
            {
                cmd.Parameters.AddWithValue("@id", nextEmployeeId);
                cmd.Parameters.AddWithValue("@lastName", lastName);
                cmd.Parameters.AddWithValue("@firstName", firstName);
                cmd.Parameters.AddWithValue("@patronymic", string.IsNullOrEmpty(patronymic) ? "" : patronymic);
                cmd.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(phone) ? "" : phone);
                cmd.Parameters.AddWithValue("@departmentId", departmentId);
                cmd.Parameters.AddWithValue("@positionId", positionId);
                cmd.Parameters.AddWithValue("@hireDate", hireDate);
                await cmd.ExecuteNonQueryAsync();
            }

            int nextAuthId;
            await using (var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(Код_автор_сотр), 0) + 1 FROM Авторизация_сотрудник", conn))
            {
                nextAuthId = (int)(await cmd.ExecuteScalarAsync() ?? 1);
            }

            const string insertAuth = @"
                INSERT INTO Авторизация_сотрудник (Код_автор_сотр, Код_сотр, Логин, Пароль, Дата_создания)
                VALUES (@authId, @employeeId, @login, @password, CURRENT_DATE)";

            await using (var cmd = new NpgsqlCommand(insertAuth, conn))
            {
                cmd.Parameters.AddWithValue("@authId", nextAuthId);
                cmd.Parameters.AddWithValue("@employeeId", nextEmployeeId);
                cmd.Parameters.AddWithValue("@login", login);
                cmd.Parameters.AddWithValue("@password", password);
                await cmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AddEmployeeAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateEmployeeAsync(int id, string lastName, string firstName, string patronymic, string phone,
        string department, string position, string role, string login, string password, DateTime hireDate)
    {
        try
        {
            await using var conn = await GetConnectionAsync();

            int departmentId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_отдела FROM Отдел WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", department);
                var result = await cmd.ExecuteScalarAsync();
                departmentId = result != null ? (int)result : 1;
            }

            int positionId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_должности FROM Должность WHERE Наименование = @name AND Роль_в_приложении = @role", conn))
            {
                cmd.Parameters.AddWithValue("@name", position);
                cmd.Parameters.AddWithValue("@role", role);
                var result = await cmd.ExecuteScalarAsync();
                positionId = result != null ? (int)result : 1;
            }

            const string updateEmployee = @"
                UPDATE Сотрудник
                SET Фамилия = @lastName, Имя = @firstName, Отчество = @patronymic,
                    Контактная_информация = @phone,
                    Код_отдела = @departmentId,
                    Код_должности = @positionId,
                    Дата_приема = @hireDate
                WHERE Код_сотр = @id";

            await using (var cmd = new NpgsqlCommand(updateEmployee, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@lastName", lastName);
                cmd.Parameters.AddWithValue("@firstName", firstName);
                cmd.Parameters.AddWithValue("@patronymic", string.IsNullOrEmpty(patronymic) ? "" : patronymic);
                cmd.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(phone) ? "" : phone);
                cmd.Parameters.AddWithValue("@departmentId", departmentId);
                cmd.Parameters.AddWithValue("@positionId", positionId);
                cmd.Parameters.AddWithValue("@hireDate", hireDate);
                await cmd.ExecuteNonQueryAsync();
            }

            if (!string.IsNullOrEmpty(password))
            {
                const string updateAuth = @"
                    UPDATE Авторизация_сотрудник
                    SET Логин = @login, Пароль = @password
                    WHERE Код_сотр = @id";
                await using (var cmd = new NpgsqlCommand(updateAuth, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@login", login);
                    cmd.Parameters.AddWithValue("@password", password);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            else if (!string.IsNullOrEmpty(login))
            {
                const string updateAuthLogin = @"
                    UPDATE Авторизация_сотрудник
                    SET Логин = @login
                    WHERE Код_сотр = @id";
                await using (var cmd = new NpgsqlCommand(updateAuthLogin, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@login", login);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateEmployeeAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        try
        {
            await using var conn = await GetConnectionAsync();
            const string query = "UPDATE Сотрудник SET Активен = false WHERE Код_сотр = @id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeleteEmployeeAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(int id, string newPassword)
    {
        try
        {
            await using var conn = await GetConnectionAsync();
            const string query = "UPDATE Авторизация_сотрудник SET Пароль = @password WHERE Код_сотр = @id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@password", newPassword);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ResetPasswordAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<string>> GetSensorTypesAsync()
    {
        var types = new List<string>();
        const string query = "SELECT Наименование FROM Тип_датчика";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                types.Add(reader.GetString(0));
        }
        catch
        {
        }
        return types;
    }

    public async Task<List<string>> GetMeasurementTypesAsync()
    {
        var types = new List<string>();
        const string query = "SELECT Наименование FROM Измерение_датчика";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                types.Add(reader.GetString(0));
        }
        catch
        {
        }
        return types;
    }

    public async Task<List<string>> GetUnitsAsync()
    {
        var units = new List<string>();
        const string query = "SELECT Наименование FROM Единица_измерения";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                units.Add(reader.GetString(0));
        }
        catch
        {
        }
        return units;
    }

    public async Task<List<string>> GetMaterialsAsync()
    {
        var materials = new List<string>();
        const string query = "SELECT Наименование FROM Материал";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                materials.Add(reader.GetString(0));
        }
        catch
        {
        }
        return materials;
    }

    public async Task<ObservableCollection<RepairTeam>> GetRepairTeamsAsync()
    {
        var teams = new ObservableCollection<RepairTeam>();
        const string query = "SELECT Код_бриг, Наименование, Специализация, Количество_рабочих FROM Бригада";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                teams.Add(new RepairTeam
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Specialization = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    WorkerCount = reader.GetInt32(3)
                });
            }
        }
        catch
        {
        }
        return teams;
    }

    public async Task<string> GeneratePipelineReportAsync(int? pipelineId = null)
    {
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand("CALL generate_pipeline_report(@pipelineId)", conn);
        cmd.Parameters.AddWithValue("@pipelineId", pipelineId ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
        return "Отчет сгенерирован в логах PostgreSQL";
    }

    public async Task<(int total, int problems, decimal avg)> GetSensorStatisticsAsync(DateTime start, DateTime end)
    {
        const string query = @"
            SELECT
                COUNT(*) as TotalMeasurements,
                COUNT(DISTINCT up.Код_уведом_проб) as Problems,
                COALESCE(AVG(z.Текущее_значение), 0) as AvgValue
            FROM Замер z
            LEFT JOIN Уведомление_проблемы up ON z.Код_замера = up.Код_замера
            WHERE z.Дата_замера BETWEEN @start AND @end";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetDecimal(2)
                );
            }
        }
        catch
        {
        }
        return (0, 0, 0);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = await GetConnectionAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Dictionary<string, int>> GetRoleStatisticsAsync()
    {
        var stats = new Dictionary<string, int>();
        const string query = @"
            SELECT Роль_в_приложении, COUNT(*)
            FROM Сотрудник s
            JOIN Должность d ON s.Код_должности = d.Код_должности
            WHERE s.Активен = true
            GROUP BY Роль_в_приложении";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats[reader.GetString(0)] = reader.GetInt32(1);
            }
        }
        catch
        {
        }
        return stats;
    }

    public async Task<ObservableCollection<DateTime>> GetAvailableDatesAsync()
    {
        var dates = new ObservableCollection<DateTime>();
        const string query = "SELECT DISTINCT Дата_замера FROM Замер WHERE Дата_замера IS NOT NULL ORDER BY Дата_замера DESC";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dates.Add(reader.GetDateTime(0));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAvailableDatesAsync error: {ex.Message}");
        }
        return dates;
    }

    public async Task<ObservableCollection<Measurement>> GetMeasurementsBySensorAndDateRangeAsync(int sensorId, DateTime startDate, DateTime endDate)
    {
        var measurements = new ObservableCollection<Measurement>();
        const string query = @"
            SELECT
                z.Код_замера,
                dt.Код_датчика,
                d.Точка_контроля,
                t.Наименование as Трубопровод,
                z.Текущее_значение,
                z.Дата_замера,
                COALESCE(z.Время_замера, '00:00:00'::time) as Время_замера,
                rd.Минимальное_значение,
                rd.Максимальное_значение,
                ei.Наименование as Единица_измерения,
                CASE
                    WHEN z.Текущее_значение > rd.Максимальное_значение THEN 'Критично'
                    WHEN z.Текущее_значение < rd.Минимальное_значение THEN 'Критично'
                    WHEN z.Текущее_значение > rd.Максимальное_значение * 0.9 THEN 'Предупреждение'
                    ELSE 'Норма'
                END as Статус
            FROM Замер z
            JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
            JOIN Датчик d ON dt.Код_датчика = d.Код_датчика
            JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
            JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
            JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
            JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
            JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
            WHERE dt.Код_датчика = @sensorId AND z.Дата_замера BETWEEN @startDate AND @endDate
            ORDER BY z.Дата_замера ASC, z.Время_замера ASC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@sensorId", sensorId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                measurements.Add(new Measurement
                {
                    Id = reader.GetInt32(0),
                    SensorId = reader.GetInt32(1),
                    SensorName = reader.GetString(2),
                    PipelineName = reader.GetString(3),
                    Value = reader.GetDecimal(4),
                    Date = reader.GetDateTime(5),
                    Time = reader.GetTimeSpan(6),
                    MinThreshold = reader.GetDecimal(7),
                    MaxThreshold = reader.GetDecimal(8),
                    Unit = reader.GetString(9),
                    Status = reader.GetString(10)
                });
            }
        }
        catch
        {
        }
        return measurements;
    }

    public async Task<bool> CreateSensorAsync(string controlPoint, string model, string manufacturer, string country,
        string sensorType, string measurementType, string unit, string minValue, string maxValue,
        int pipelineId, string location, string productionYear)
    {
        try
        {
            await using var conn = await GetConnectionAsync();

            int manufacturerId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_производ FROM Производитель WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", manufacturer);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    manufacturerId = (int)result;
                }
                else
                {
                    await using (var insertCmd = new NpgsqlCommand("INSERT INTO Производитель (Код_производ, Наименование, Страна) VALUES ((SELECT COALESCE(MAX(Код_производ), 0) + 1 FROM Производитель), @name, @country) RETURNING Код_производ", conn))
                    {
                        insertCmd.Parameters.AddWithValue("@name", manufacturer);
                        insertCmd.Parameters.AddWithValue("@country", country);
                        manufacturerId = (int)(await insertCmd.ExecuteScalarAsync() ?? 1);
                    }
                }
            }

            int modelId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_модели FROM Модель WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", model);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    modelId = (int)result;
                }
                else
                {
                    await using (var insertCmd = new NpgsqlCommand("INSERT INTO Модель (Код_модели, Наименование, Код_производ) VALUES ((SELECT COALESCE(MAX(Код_модели), 0) + 1 FROM Модель), @name, @manufacturerId) RETURNING Код_модели", conn))
                    {
                        insertCmd.Parameters.AddWithValue("@name", model);
                        insertCmd.Parameters.AddWithValue("@manufacturerId", manufacturerId);
                        modelId = (int)(await insertCmd.ExecuteScalarAsync() ?? 1);
                    }
                }
            }

            int sensorTypeId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_типа_дат FROM Тип_датчика WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", sensorType);
                var result = await cmd.ExecuteScalarAsync();
                sensorTypeId = result != null ? (int)result : 1;
            }

            int measurementTypeId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_измер_дат FROM Измерение_датчика WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", measurementType);
                var result = await cmd.ExecuteScalarAsync();
                measurementTypeId = result != null ? (int)result : 1;
            }

            int unitId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_ед_измер FROM Единица_измерения WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", unit);
                var result = await cmd.ExecuteScalarAsync();
                unitId = result != null ? (int)result : 1;
            }

            int measurementFeatureId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_особ_измер_дат FROM Особенности_измерения WHERE Код_измер_дат = @measurementTypeId AND Код_ед_измер = @unitId", conn))
            {
                cmd.Parameters.AddWithValue("@measurementTypeId", measurementTypeId);
                cmd.Parameters.AddWithValue("@unitId", unitId);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    measurementFeatureId = (int)result;
                }
                else
                {
                    await using (var insertCmd = new NpgsqlCommand("INSERT INTO Особенности_измерения (Код_особ_измер_дат, Наименование, Код_измер_дат, Код_ед_измер) VALUES ((SELECT COALESCE(MAX(Код_особ_измер_дат), 0) + 1 FROM Особенности_измерения), @name, @measurementTypeId, @unitId) RETURNING Код_особ_измер_дат", conn))
                    {
                        insertCmd.Parameters.AddWithValue("@name", $"{measurementType} через {unit}");
                        insertCmd.Parameters.AddWithValue("@measurementTypeId", measurementTypeId);
                        insertCmd.Parameters.AddWithValue("@unitId", unitId);
                        measurementFeatureId = (int)(await insertCmd.ExecuteScalarAsync() ?? 1);
                    }
                }
            }

            int workId;
            await using (var cmd = new NpgsqlCommand("INSERT INTO Работа_датчика (Код_раб_дат, Описание, Минимальное_значение, Максимальное_значение) VALUES ((SELECT COALESCE(MAX(Код_раб_дат), 0) + 1 FROM Работа_датчика), @description, @minValue, @maxValue) RETURNING Код_раб_дат", conn))
            {
                cmd.Parameters.AddWithValue("@description", $"Датчик {controlPoint}");
                cmd.Parameters.AddWithValue("@minValue", decimal.Parse(minValue));
                cmd.Parameters.AddWithValue("@maxValue", decimal.Parse(maxValue));
                workId = (int)(await cmd.ExecuteScalarAsync() ?? 1);
            }

            int sensorFeatureId;
            await using (var cmd = new NpgsqlCommand("INSERT INTO Особенности_датчика (Код_особ_дат, Код_раб_дат, Код_типа_дат) VALUES ((SELECT COALESCE(MAX(Код_особ_дат), 0) + 1 FROM Особенности_датчика), @workId, @sensorTypeId) RETURNING Код_особ_дат", conn))
            {
                cmd.Parameters.AddWithValue("@workId", workId);
                cmd.Parameters.AddWithValue("@sensorTypeId", sensorTypeId);
                sensorFeatureId = (int)(await cmd.ExecuteScalarAsync() ?? 1);
            }

            int year = string.IsNullOrEmpty(productionYear) ? 0 : int.Parse(productionYear);

            int sensorId;
            await using (var cmd = new NpgsqlCommand("INSERT INTO Датчик (Код_датчика, Код_модели, Код_особ_дат, Код_особ_измер_дат, Точка_контроля, Год_выпуска) VALUES ((SELECT COALESCE(MAX(Код_датчика), 0) + 1 FROM Датчик), @modelId, @sensorFeatureId, @measurementFeatureId, @controlPoint, @year) RETURNING Код_датчика", conn))
            {
                cmd.Parameters.AddWithValue("@modelId", modelId);
                cmd.Parameters.AddWithValue("@sensorFeatureId", sensorFeatureId);
                cmd.Parameters.AddWithValue("@measurementFeatureId", measurementFeatureId);
                cmd.Parameters.AddWithValue("@controlPoint", controlPoint);
                cmd.Parameters.AddWithValue("@year", year);
                sensorId = (int)(await cmd.ExecuteScalarAsync() ?? 1);
            }

            if (pipelineId > 0 && !string.IsNullOrEmpty(location))
            {
                await using (var cmd = new NpgsqlCommand("INSERT INTO Датчик_трубопровод (Код_дат_труб, Код_датчика, Код_трубопровода, Дата_установки, Местоположение) VALUES ((SELECT COALESCE(MAX(Код_дат_труб), 0) + 1 FROM Датчик_трубопровод), @sensorId, @pipelineId, CURRENT_DATE, @location)", conn))
                {
                    cmd.Parameters.AddWithValue("@sensorId", sensorId);
                    cmd.Parameters.AddWithValue("@pipelineId", pipelineId);
                    cmd.Parameters.AddWithValue("@location", location);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreatePipelineAsync(string name, string material, string length, string diameter, DateTime installationDate)
    {
        try
        {
            await using var conn = await GetConnectionAsync();

            int materialId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_материала FROM Материал WHERE Наименование = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", material);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null) return false;
                materialId = (int)result;
            }

            int viewId;
            await using (var cmd = new NpgsqlCommand("SELECT Код_вида_труб FROM Вид_трубопровода WHERE Код_материала = @materialId", conn))
            {
                cmd.Parameters.AddWithValue("@materialId", materialId);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    viewId = (int)result;
                    await using (var updateCmd = new NpgsqlCommand("UPDATE Вид_трубопровода SET Протяженность = @length, Диаметр = @diameter WHERE Код_вида_труб = @viewId", conn))
                    {
                        updateCmd.Parameters.AddWithValue("@length", decimal.Parse(length));
                        updateCmd.Parameters.AddWithValue("@diameter", decimal.Parse(diameter));
                        updateCmd.Parameters.AddWithValue("@viewId", viewId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    await using (var insertCmd = new NpgsqlCommand("INSERT INTO Вид_трубопровода (Код_вида_труб, Код_материала, Протяженность, Диаметр) VALUES ((SELECT COALESCE(MAX(Код_вида_труб), 0) + 1 FROM Вид_трубопровода), @materialId, @length, @diameter) RETURNING Код_вида_труб", conn))
                    {
                        insertCmd.Parameters.AddWithValue("@materialId", materialId);
                        insertCmd.Parameters.AddWithValue("@length", decimal.Parse(length));
                        insertCmd.Parameters.AddWithValue("@diameter", decimal.Parse(diameter));
                        viewId = (int)(await insertCmd.ExecuteScalarAsync() ?? 1);
                    }
                }
            }

            await using (var cmd = new NpgsqlCommand("INSERT INTO Трубопровод (Код_трубопровода, Наименование, Дата_установки, Код_вида_труб) VALUES ((SELECT COALESCE(MAX(Код_трубопровода), 0) + 1 FROM Трубопровод), @name, @date, @viewId)", conn))
            {
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@date", installationDate);
                cmd.Parameters.AddWithValue("@viewId", viewId);
                await cmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ObservableCollection<Equipment>> GetAllEquipmentAsync()
    {
        var equipment = new ObservableCollection<Equipment>();
        const string query = @"
        SELECT
            d.Код_датчика,
            m.Наименование as Модель,
            p.Наименование as Производитель,
            td.Наименование as Тип,
            COALESCE(dt.Местоположение, 'Не указано') as Местоположение,
            p.Страна,
            d.Точка_контроля,
            COALESCE(t.Наименование, 'Не установлен') as Трубопровод,
            d.Год_выпуска,
            d.Дата_последней_поверки,
            rd.Минимальное_значение,
            rd.Максимальное_значение,
            ei.Наименование as Единица_измерения,
            dim.Наименование as Измерение
        FROM Датчик d
        JOIN Модель m ON d.Код_модели = m.Код_модели
        JOIN Производитель p ON m.Код_производ = p.Код_производ
        JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
        JOIN Тип_датчика td ON od.Код_типа_дат = td.Код_типа_дат
        JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
        JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
        JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
        JOIN Измерение_датчика dim ON oi.Код_измер_дат = dim.Код_измер_дат
        LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
        ORDER BY d.Код_датчика";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                equipment.Add(new Equipment
                {
                    Id = reader.GetInt32(0),
                    Model = reader.GetString(1),
                    Manufacturer = reader.GetString(2),
                    Type = reader.GetString(3),
                    InstallationLocation = reader.GetString(4),
                    ManufacturerCountry = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ControlPoint = reader.IsDBNull(6) ? null : reader.GetString(6),
                    PipelineName = reader.GetString(7),
                    ProductionYear = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    LastCalibration = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    MinValue = reader.GetDecimal(10),
                    MaxValue = reader.GetDecimal(11),
                    Unit = reader.IsDBNull(12) ? null : reader.GetString(12),
                    MeasurementType = reader.IsDBNull(13) ? null : reader.GetString(13)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllEquipmentAsync error: {ex.Message}");
        }
        return equipment;
    }

    public async Task<ObservableCollection<Equipment>> SearchEquipmentByNameAsync(string searchTerm)
    {
        var equipment = new ObservableCollection<Equipment>();
        const string query = @"
        SELECT
            d.Код_датчика,
            m.Наименование as Модель,
            p.Наименование as Производитель,
            td.Наименование as Тип,
            COALESCE(dt.Местоположение, 'Не указано') as Местоположение,
            p.Страна,
            d.Точка_контроля,
            COALESCE(t.Наименование, 'Не установлен') as Трубопровод,
            d.Год_выпуска,
            d.Дата_последней_поверки,
            rd.Минимальное_значение,
            rd.Максимальное_значение,
            ei.Наименование as Единица_измерения,
            dim.Наименование as Измерение
        FROM Датчик d
        JOIN Модель m ON d.Код_модели = m.Код_модели
        JOIN Производитель p ON m.Код_производ = p.Код_производ
        JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
        JOIN Тип_датчика td ON od.Код_типа_дат = td.Код_типа_дат
        JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
        JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
        JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
        JOIN Измерение_датчика dim ON oi.Код_измер_дат = dim.Код_измер_дат
        LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
        WHERE m.Наименование ILIKE @search 
           OR p.Наименование ILIKE @search
           OR td.Наименование ILIKE @search
        ORDER BY d.Код_датчика";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@search", $"%{searchTerm}%");
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                equipment.Add(new Equipment
                {
                    Id = reader.GetInt32(0),
                    Model = reader.GetString(1),
                    Manufacturer = reader.GetString(2),
                    Type = reader.GetString(3),
                    InstallationLocation = reader.GetString(4),
                    ManufacturerCountry = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ControlPoint = reader.IsDBNull(6) ? null : reader.GetString(6),
                    PipelineName = reader.GetString(7),
                    ProductionYear = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    LastCalibration = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    MinValue = reader.GetDecimal(10),
                    MaxValue = reader.GetDecimal(11),
                    Unit = reader.IsDBNull(12) ? null : reader.GetString(12),
                    MeasurementType = reader.IsDBNull(13) ? null : reader.GetString(13)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SearchEquipmentByNameAsync error: {ex.Message}");
        }
        return equipment;
    }

    public async Task<Equipment?> GetEquipmentDetailsAsync(int equipmentId)
    {
        const string query = @"
        SELECT
            d.Код_датчика,
            m.Наименование as Модель,
            p.Наименование as Производитель,
            td.Наименование as Тип,
            COALESCE(dt.Местоположение, 'Не указано') as Местоположение,
            p.Страна,
            d.Точка_контроля,
            COALESCE(t.Наименование, 'Не установлен') as Трубопровод,
            d.Год_выпуска,
            d.Дата_последней_поверки,
            rd.Минимальное_значение,
            rd.Максимальное_значение,
            ei.Наименование as Единица_измерения,
            dim.Наименование as Измерение
        FROM Датчик d
        JOIN Модель m ON d.Код_модели = m.Код_модели
        JOIN Производитель p ON m.Код_производ = p.Код_производ
        JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
        JOIN Тип_датчика td ON od.Код_типа_дат = td.Код_типа_дат
        JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
        JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
        JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
        JOIN Измерение_датчика dim ON oi.Код_измер_дат = dim.Код_измер_дат
        LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
        WHERE d.Код_датчика = @equipmentId";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@equipmentId", equipmentId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Equipment
                {
                    Id = reader.GetInt32(0),
                    Model = reader.GetString(1),
                    Manufacturer = reader.GetString(2),
                    Type = reader.GetString(3),
                    InstallationLocation = reader.GetString(4),
                    ManufacturerCountry = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ControlPoint = reader.IsDBNull(6) ? null : reader.GetString(6),
                    PipelineName = reader.GetString(7),
                    ProductionYear = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    LastCalibration = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    MinValue = reader.GetDecimal(10),
                    MaxValue = reader.GetDecimal(11),
                    Unit = reader.IsDBNull(12) ? null : reader.GetString(12),
                    MeasurementType = reader.IsDBNull(13) ? null : reader.GetString(13)
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetEquipmentDetailsAsync error: {ex.Message}");
        }
        return null;
    }

    public async Task<List<string>> GetDepartmentsAsync()
    {
        var departments = new List<string>();
        const string query = "SELECT Наименование FROM Отдел ORDER BY Наименование";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                departments.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetDepartmentsAsync error: {ex.Message}");
        }
        return departments;
    }

    public async Task<List<string>> GetPositionsByRoleAsync(string role)
    {
        var positions = new List<string>();
        const string query = "SELECT Наименование FROM Должность WHERE Роль_в_приложении = @role ORDER BY Наименование";
        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@role", role);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                positions.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetPositionsByRoleAsync error: {ex.Message}");
        }
        return positions;
    }

    public async Task<bool> RestoreEmployeeAsync(int id)
    {
        try
        {
            await using var conn = await GetConnectionAsync();
            const string query = "UPDATE Сотрудник SET Активен = true WHERE Код_сотр = @id";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RestoreEmployeeAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<ObservableCollection<Inspection>> GetAllInspectionsForAdminAsync()
    {
        var inspections = new ObservableCollection<Inspection>();
        const string query = @"
        SELECT
            p.Код_проверки,
            p.Код_уведом_проб,
            p.Код_сотр,
            CONCAT(s.Фамилия, ' ', s.Имя, ' ', COALESCE(s.Отчество, '')) as Сотрудник,
            p.Дата_начала,
            p.Дата_окончания,
            st.Наименование as Статус,
            p.Описание,
            up.Описание as Проблема_описание,
            tp.Наименование as Тип_проблемы,
            up.Дата_уведомления,
            up.Время_уведомления
        FROM Проверка p
        JOIN Сотрудник s ON p.Код_сотр = s.Код_сотр
        JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
        JOIN Уведомление_проблемы up ON p.Код_уведом_проб = up.Код_уведом_проб
        JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
        ORDER BY p.Дата_начала DESC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                inspections.Add(new Inspection
                {
                    Id = reader.GetInt32(0),
                    ProblemId = reader.GetInt32(1),
                    EmployeeId = reader.GetInt32(2),
                    EmployeeName = reader.GetString(3),
                    StartDate = reader.GetDateTime(4),
                    EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    Description = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    ProblemDescription = reader.GetString(8),
                    ProblemType = reader.GetString(9)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllInspectionsForAdminAsync error: {ex.Message}");
        }
        return inspections;
    }

    public async Task<string> GetInspectionHistoryAsync(int inspectionId)
    {
        var history = "";
        const string query = @"
        SELECT 
            p.Дата_начала as Дата_изменения,
            NULL::time as Время_изменения,
            st.Наименование as Статус,
            COALESCE(p.Описание, '') as Комментарий,
            1 as Порядок
        FROM Проверка p
        JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
        WHERE p.Код_проверки = @inspectionId
        
        UNION ALL
        
        SELECT 
            p.Дата_окончания as Дата_изменения,
            NULL::time as Время_изменения,
            CONCAT('Завершено: ', st.Наименование) as Статус,
            COALESCE(p.Описание, '') as Комментарий,
            2 as Порядок
        FROM Проверка p
        JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
        WHERE p.Код_проверки = @inspectionId
        AND p.Дата_окончания IS NOT NULL
        
        ORDER BY Дата_изменения ASC, Порядок ASC";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@inspectionId", inspectionId);
            await using var reader = await cmd.ExecuteReaderAsync();

            bool hasRows = false;
            while (await reader.ReadAsync())
            {
                hasRows = true;
                var changeDate = reader.GetDateTime(0);
                var status = reader.GetString(2);
                var comment = reader.GetString(3);
                history += $"• {changeDate:dd.MM.yyyy} → {status}";
                if (!string.IsNullOrEmpty(comment))
                    history += $" (Комментарий: {comment})";
                history += "\n";
            }

            if (!hasRows)
            {
                const string currentStatusQuery = @"
                SELECT st.Наименование, p.Дата_начала
                FROM Проверка p
                JOIN Статус_проверки st ON p.Код_статуса_проверки = st.Код_статуса_проверки
                WHERE p.Код_проверки = @inspectionId";

                await using var cmd2 = new NpgsqlCommand(currentStatusQuery, conn);
                cmd2.Parameters.AddWithValue("@inspectionId", inspectionId);
                await using var reader2 = await cmd2.ExecuteReaderAsync();
                if (await reader2.ReadAsync())
                {
                    history = $"• {reader2.GetDateTime(1):dd.MM.yyyy} → {reader2.GetString(0)} (Текущий статус)\n";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetInspectionHistoryAsync error: {ex.Message}");
            history = "История изменений статуса недоступна\n";
        }
        return string.IsNullOrEmpty(history) ? "Нет записей об изменениях статуса\n" : history;
    }

    public async Task<ObservableCollection<Inspection>> GetEquipmentForReportAsync()
    {
        var equipment = new ObservableCollection<Inspection>();
        const string query = @"
        SELECT DISTINCT
            d.Код_датчика,
            CONCAT(m.Наименование, ' - ', COALESCE(t.Наименование, 'Не установлен')) as Название,
            0 as Код_уведом_проб,
            '' as Сотрудник,
            CURRENT_DATE as Дата_начала,
            NULL as Дата_окончания,
            '' as Статус,
            '' as Описание,
            '' as Проблема_описание,
            '' as Тип_проблемы
        FROM Датчик d
        JOIN Модель m ON d.Код_модели = m.Код_модели
        LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
        ORDER BY m.Наименование";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                equipment.Add(new Inspection
                {
                    Id = reader.GetInt32(0),
                    EmployeeName = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetEquipmentForReportAsync error: {ex.Message}");
        }
        return equipment;
    }

    public async Task<ObservableCollection<SensorForDropdown>> GetSensorsForDropdownAsync()
    {
        var sensors = new ObservableCollection<SensorForDropdown>();
        const string query = @"
        SELECT 
            d.Код_датчика,
            CONCAT(m.Наименование, ' (', td.Наименование, ') - ', COALESCE(t.Наименование, 'Не установлен')) as Название
        FROM Датчик d
        JOIN Модель m ON d.Код_модели = m.Код_модели
        JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
        JOIN Тип_датчика td ON od.Код_типа_дат = td.Код_типа_дат
        LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
        ORDER BY m.Наименование";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sensors.Add(new SensorForDropdown
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetSensorsForDropdownAsync error: {ex.Message}");
        }
        return sensors;
    }

    public async Task<EquipmentForReport?> GetEquipmentReportByIdAsync(int equipmentId)
    {
        const string query = @"
    SELECT 
        d.Код_датчика,
        m.Наименование as Модель,
        p.Наименование as Производитель,
        COALESCE(p.Страна, '') as Страна,
        td.Наименование as Тип,
        COALESCE(dt.Местоположение, 'Не указано') as Местоположение,
        COALESCE(t.Наименование, 'Не установлен') as Трубопровод,
        ei.Наименование as Единица_измерения,
        rd.Минимальное_значение,
        rd.Максимальное_значение,
        COALESCE(dim.Наименование, '') as Измерение,
        d.Год_выпуска,
        d.Дата_последней_поверки,
        COALESCE(d.Точка_контроля, '') as Точка_контроля,
        (
            SELECT z.Текущее_значение 
            FROM Замер z 
            JOIN Датчик_трубопровод dt2 ON z.Код_дат_труб = dt2.Код_дат_труб
            WHERE dt2.Код_датчика = d.Код_датчика 
            ORDER BY z.Дата_замера DESC, z.Время_замера DESC 
            LIMIT 1
        ) as Последнее_значение,
        (
            SELECT CONCAT(TO_CHAR(z.Дата_замера, 'DD.MM.YYYY'), ' ', z.Время_замера)
            FROM Замер z 
            JOIN Датчик_трубопровод dt2 ON z.Код_дат_труб = dt2.Код_дат_труб
            WHERE dt2.Код_датчика = d.Код_датчика 
            ORDER BY z.Дата_замера DESC, z.Время_замера DESC 
            LIMIT 1
        ) as Дата_время_последнего_замера,
        (
            SELECT string_agg(repair_info, ', ')
            FROM (
                SELECT CONCAT(sr.Наименование, ' (', TO_CHAR(r.Дата_начала, 'DD.MM.YYYY'), ')') as repair_info
                FROM Ремонт r
                JOIN Проверка p2 ON r.Код_проверки = p2.Код_проверки
                JOIN Уведомление_проблемы up ON p2.Код_уведом_проб = up.Код_уведом_проб
                JOIN Замер z ON up.Код_замера = z.Код_замера
                JOIN Датчик_трубопровод dt2 ON z.Код_дат_труб = dt2.Код_дат_труб
                JOIN Статус_ремонта sr ON r.Код_статуса_ремонта = sr.Код_статуса_ремонта
                WHERE dt2.Код_датчика = d.Код_датчика 
                ORDER BY r.Дата_начала DESC 
                LIMIT 3
            ) sub
        ) as Последние_ремонты
    FROM Датчик d
    JOIN Модель m ON d.Код_модели = m.Код_модели
    JOIN Производитель p ON m.Код_производ = p.Код_производ
    JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
    JOIN Тип_датчика td ON od.Код_типа_дат = td.Код_типа_дат
    JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
    JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
    JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
    JOIN Измерение_датчика dim ON oi.Код_измер_дат = dim.Код_измер_дат
    LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
    LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода
    WHERE d.Код_датчика = @equipmentId";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@equipmentId", equipmentId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var equipment = new EquipmentForReport();
                equipment.Id = reader.GetInt32(0);
                equipment.Model = reader.GetString(1);
                equipment.Manufacturer = reader.GetString(2);
                equipment.ManufacturerCountry = reader.IsDBNull(3) ? "" : reader.GetString(3);
                equipment.Type = reader.GetString(4);
                equipment.InstallationLocation = reader.IsDBNull(5) ? "" : reader.GetString(5);
                equipment.PipelineName = reader.IsDBNull(6) ? "" : reader.GetString(6);
                equipment.Unit = reader.IsDBNull(7) ? "" : reader.GetString(7);
                equipment.MinThreshold = reader.GetDecimal(8);
                equipment.MaxThreshold = reader.GetDecimal(9);
                equipment.MeasurementType = reader.IsDBNull(10) ? "" : reader.GetString(10);
                equipment.ProductionYear = reader.IsDBNull(11) ? null : reader.GetInt32(11);
                equipment.LastCalibration = reader.IsDBNull(12) ? null : reader.GetDateTime(12);
                equipment.ControlPoint = reader.IsDBNull(13) ? "" : reader.GetString(13);
                equipment.LastMeasurementValue = reader.IsDBNull(14) ? null : reader.GetDecimal(14);

                if (!reader.IsDBNull(15))
                {
                    string dateTimeStr = reader.GetString(15);
                    if (DateTime.TryParse(dateTimeStr, out DateTime parsedDate))
                    {
                        equipment.LastMeasurementDate = parsedDate;
                    }
                }

                equipment.LastRepairDescription = reader.IsDBNull(16) ? "Нет данных о ремонтах" : reader.GetString(16);
                equipment.Name = $"{equipment.Model} ({equipment.Type})";

                return equipment;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetEquipmentReportByIdAsync error: {ex.Message}");
        }
        return null;
    }

    public async Task<SensorStatisticsResult> GetSensorStatisticsByIdAsync(int sensorId, DateTime startDate, DateTime endDate)
    {
        var stats = new SensorStatisticsResult
        {
            StartDate = startDate,
            EndDate = endDate,
            DailyStats = new List<SensorDailyStat>()
        };

        const string query = @"
        SELECT 
            COUNT(*) as TotalMeasurements,
            COUNT(DISTINCT up.Код_уведом_проб) as Problems,
            COALESCE(AVG(z.Текущее_значение), 0) as AvgValue
        FROM Замер z
        JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
        LEFT JOIN Уведомление_проблемы up ON z.Код_замера = up.Код_замера
        WHERE dt.Код_датчика = @sensorId 
            AND z.Дата_замера BETWEEN @startDate AND @endDate";

        const string dailyQuery = @"
        SELECT 
            z.Дата_замера,
            COALESCE(AVG(z.Текущее_значение), 0) as AvgValue,
            COUNT(*) as MeasurementCount,
            COUNT(DISTINCT up.Код_уведом_проб) as ProblemCount
        FROM Замер z
        JOIN Датчик_трубопровод dt ON z.Код_дат_труб = dt.Код_дат_труб
        LEFT JOIN Уведомление_проблемы up ON z.Код_замера = up.Код_замера
        WHERE dt.Код_датчика = @sensorId 
            AND z.Дата_замера BETWEEN @startDate AND @endDate
        GROUP BY z.Дата_замера
        ORDER BY z.Дата_замера";

        try
        {
            await using var conn = await GetConnectionAsync();

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@sensorId", sensorId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.TotalMeasurements = reader.GetInt32(0);
                stats.ProblemCount = reader.GetInt32(1);
                stats.AverageValue = reader.GetDecimal(2);
            }
            await reader.CloseAsync();

            await using var dailyCmd = new NpgsqlCommand(dailyQuery, conn);
            dailyCmd.Parameters.AddWithValue("@sensorId", sensorId);
            dailyCmd.Parameters.AddWithValue("@startDate", startDate);
            dailyCmd.Parameters.AddWithValue("@endDate", endDate);
            await using var dailyReader = await dailyCmd.ExecuteReaderAsync();
            while (await dailyReader.ReadAsync())
            {
                stats.DailyStats.Add(new SensorDailyStat
                {
                    Date = dailyReader.GetDateTime(0),
                    AverageValue = dailyReader.GetDecimal(1),
                    MeasurementCount = dailyReader.GetInt32(2),
                    ProblemCount = dailyReader.GetInt32(3)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetSensorStatisticsByIdAsync error: {ex.Message}");
        }
        return stats;
    }

    public async Task<ComprehensiveReport> GetComprehensiveReportAsync(DateTime startDate, DateTime endDate)
    {
        var report = new ComprehensiveReport
        {
            StartDate = startDate,
            EndDate = endDate,
            GeneratedAt = DateTime.Now,
            EquipmentList = new List<EquipmentForReport>(),
            ProblemSummary = new List<ProblemSummary>()
        };

        const string equipmentQuery = @"
        SELECT 
            d.Код_датчика,
            m.Наименование as Модель,
            p.Наименование as Производитель,
            COALESCE(p.Страна, '') as Страна,
            td.Наименование as Тип,
            COALESCE(dt.Местоположение, 'Не указано') as Местоположение,
            COALESCE(t.Наименование, 'Не установлен') as Трубопровод,
            ei.Наименование as Единица_измерения,
            rd.Минимальное_значение,
            rd.Максимальное_значение,
            COALESCE(dim.Наименование, '') as Измерение,
            d.Год_выпуска,
            d.Дата_последней_поверки,
            COALESCE(d.Точка_контроля, '') as Точка_контроля
        FROM Датчик d
        JOIN Модель m ON d.Код_модели = m.Код_модели
        JOIN Производитель p ON m.Код_производ = p.Код_производ
        JOIN Особенности_датчика od ON d.Код_особ_дат = od.Код_особ_дат
        JOIN Тип_датчика td ON od.Код_типа_дат = td.Код_типа_дат
        JOIN Работа_датчика rd ON od.Код_раб_дат = rd.Код_раб_дат
        JOIN Особенности_измерения oi ON d.Код_особ_измер_дат = oi.Код_особ_измер_дат
        JOIN Единица_измерения ei ON oi.Код_ед_измер = ei.Код_ед_измер
        JOIN Измерение_датчика dim ON oi.Код_измер_дат = dim.Код_измер_дат
        LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
        LEFT JOIN Трубопровод t ON dt.Код_трубопровода = t.Код_трубопровода";

        const string summaryQuery = @"
        SELECT 
            COUNT(DISTINCT d.Код_датчика) as TotalSensors,
            COUNT(DISTINCT z.Код_замера) as TotalMeasurements,
            COUNT(DISTINCT up.Код_уведом_проб) as TotalProblems,
            COUNT(DISTINCT p.Код_проверки) as TotalInspections,
            COUNT(DISTINCT r.Код_ремонта) as TotalRepairs,
            COALESCE(AVG(z.Текущее_значение), 0) as AvgMeasurementValue
        FROM Датчик d
        LEFT JOIN Датчик_трубопровод dt ON d.Код_датчика = dt.Код_датчика
        LEFT JOIN Замер z ON dt.Код_дат_труб = z.Код_дат_труб AND z.Дата_замера BETWEEN @startDate AND @endDate
        LEFT JOIN Уведомление_проблемы up ON z.Код_замера = up.Код_замера
        LEFT JOIN Проверка p ON up.Код_уведом_проб = p.Код_уведом_проб
        LEFT JOIN Ремонт r ON p.Код_проверки = r.Код_проверки";

        const string problemSummaryQuery = @"
        SELECT 
            tp.Наименование as ProblemType,
            COUNT(*) as Count,
            COALESCE(tp.Категория_риска, 'Средний') as RiskCategory
        FROM Уведомление_проблемы up
        JOIN Тип_проблемы tp ON up.Код_типа_проблемы = tp.Код_типа_проблемы
        WHERE up.Дата_уведомления BETWEEN @startDate AND @endDate
        GROUP BY tp.Наименование, tp.Категория_риска
        ORDER BY Count DESC";

        try
        {
            await using var conn = await GetConnectionAsync();

            await using var summaryCmd = new NpgsqlCommand(summaryQuery, conn);
            summaryCmd.Parameters.AddWithValue("@startDate", startDate);
            summaryCmd.Parameters.AddWithValue("@endDate", endDate);
            await using var summaryReader = await summaryCmd.ExecuteReaderAsync();
            if (await summaryReader.ReadAsync())
            {
                report.TotalSensors = summaryReader.GetInt32(0);
                report.TotalMeasurements = summaryReader.GetInt32(1);
                report.TotalProblems = summaryReader.GetInt32(2);
                report.TotalInspections = summaryReader.GetInt32(3);
                report.TotalRepairs = summaryReader.GetInt32(4);
                report.AverageMeasurementValue = summaryReader.GetDecimal(5);
            }
            await summaryReader.CloseAsync();

            await using var equipmentCmd = new NpgsqlCommand(equipmentQuery, conn);
            await using var equipmentReader = await equipmentCmd.ExecuteReaderAsync();
            while (await equipmentReader.ReadAsync())
            {
                var equipment = new EquipmentForReport();
                equipment.Id = equipmentReader.GetInt32(0);
                equipment.Model = equipmentReader.GetString(1);
                equipment.Manufacturer = equipmentReader.GetString(2);
                equipment.ManufacturerCountry = equipmentReader.GetString(3);
                equipment.Type = equipmentReader.GetString(4);
                equipment.InstallationLocation = equipmentReader.GetString(5);
                equipment.PipelineName = equipmentReader.GetString(6);
                equipment.Unit = equipmentReader.GetString(7);
                equipment.MinThreshold = equipmentReader.GetDecimal(8);
                equipment.MaxThreshold = equipmentReader.GetDecimal(9);
                equipment.MeasurementType = equipmentReader.GetString(10);
                equipment.ProductionYear = equipmentReader.IsDBNull(11) ? null : equipmentReader.GetInt32(11);
                equipment.LastCalibration = equipmentReader.IsDBNull(12) ? null : equipmentReader.GetDateTime(12);
                equipment.ControlPoint = equipmentReader.GetString(13);
                equipment.Name = $"{equipment.Model} ({equipment.Type})";
                report.EquipmentList.Add(equipment);
            }
            await equipmentReader.CloseAsync();

            await using var problemCmd = new NpgsqlCommand(problemSummaryQuery, conn);
            problemCmd.Parameters.AddWithValue("@startDate", startDate);
            problemCmd.Parameters.AddWithValue("@endDate", endDate);
            await using var problemReader = await problemCmd.ExecuteReaderAsync();
            while (await problemReader.ReadAsync())
            {
                report.ProblemSummary.Add(new ProblemSummary
                {
                    ProblemType = problemReader.GetString(0),
                    Count = problemReader.GetInt32(1),
                    RiskCategory = problemReader.GetString(2)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetComprehensiveReportAsync error: {ex.Message}");
        }
        return report;
    }

    // ИСПРАВЛЕННЫЙ метод для создания проверки со статусом
    public async Task<bool> CreateInspectionWithStatusAsync(int problemId, int employeeId, string description, string urgency, string status)
    {
        const string query = @"
        INSERT INTO Проверка (Код_проверки, Код_сотр, Код_уведом_проб, Дата_начала, Код_статуса_проверки, Описание)
        VALUES ((SELECT COALESCE(MAX(Код_проверки), 0) + 1 FROM Проверка),
                @employeeId, @problemId, CURRENT_DATE, 
                (SELECT Код_статуса_проверки FROM Статус_проверки WHERE Наименование = @status),
                @description)";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            cmd.Parameters.AddWithValue("@problemId", problemId);
            cmd.Parameters.AddWithValue("@description", $"{description} (Срочность: {urgency})");
            cmd.Parameters.AddWithValue("@status", status);
            int result = await cmd.ExecuteNonQueryAsync();

            if (result > 0)
            {
                // Также обновляем статус проблемы
                await UpdateProblemStatusWithUrgencyAsync(problemId, status, urgency);
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CreateInspectionWithStatusAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<ObservableCollection<InspectorItem>> GetInspectorsAsync()
    {
        var inspectors = new ObservableCollection<InspectorItem>();
        const string query = @"
        SELECT Код_сотр, TRIM(CONCAT(Фамилия, ' ', Имя, ' ', COALESCE(Отчество, ''))) as FullName
        FROM Сотрудник
        WHERE Код_должности IN (SELECT Код_должности FROM Должность WHERE Роль_в_приложении = 'Инспектор')
        AND Активен = true";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                inspectors.Add(new InspectorItem { Id = reader.GetInt32(0), FullName = reader.GetString(1).Trim() });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetInspectorsAsync error: {ex.Message}");
        }
        return inspectors;
    }

    public async Task<ObservableCollection<RepairManagerItem>> GetRepairManagersAsync()
    {
        var managers = new ObservableCollection<RepairManagerItem>();
        const string query = @"
        SELECT Код_сотр, TRIM(CONCAT(Фамилия, ' ', Имя, ' ', COALESCE(Отчество, ''))) as FullName
        FROM Сотрудник
        WHERE Код_должности IN (SELECT Код_должности FROM Должность WHERE Роль_в_приложении = 'Начальник_ремонтной_службы')
        AND Активен = true";

        try
        {
            await using var conn = await GetConnectionAsync();
            await using var cmd = new NpgsqlCommand(query, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                managers.Add(new RepairManagerItem { Id = reader.GetInt32(0), FullName = reader.GetString(1).Trim() });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetRepairManagersAsync error: {ex.Message}");
        }
        return managers;
    }
}