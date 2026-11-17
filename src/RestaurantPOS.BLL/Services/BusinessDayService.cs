using Microsoft.EntityFrameworkCore;
using RestaurantPOS.BLL.Interfaces;
using RestaurantPOS.DAL.Data;
using RestaurantPOS.DAL.Entities;

namespace RestaurantPOS.BLL.Services;

public class BusinessDayService : IBusinessDayService
{
    private readonly ApplicationDbContext _db;
    private static readonly TimeZoneInfo PakistanTimeZone = ResolvePakistanTimeZone();

    public BusinessDayService(ApplicationDbContext db)
    {
        _db = db;
    }

    private static TimeZoneInfo ResolvePakistanTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "Pakistan Standard Time", "Pakistan Standard Time");
            }
        }
    }

    /// <summary>
    /// Returns current timestamp in Pakistan Standard Time (Asia/Karachi, UTC+5).
    /// </summary>
    public DateTime GetNowPakistanTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PakistanTimeZone);
    }

    /// <summary>
    /// Centralized BusinessDate Rule:
    /// - If transaction time is between 12:00:00 AM and 03:59:59.999 AM, BusinessDate = Previous Calendar Date.
    /// - Otherwise (04:00:00 AM to 11:59:59.999 PM), BusinessDate = Current Calendar Date.
    /// Example:
    ///   - 17 Nov 2026 07:15 PM -> BusinessDate = 17 Nov 2026
    ///   - 18 Nov 2026 12:30 AM -> BusinessDate = 17 Nov 2026
    ///   - 18 Nov 2026 03:30 AM -> BusinessDate = 17 Nov 2026
    ///   - 18 Nov 2026 04:00 AM -> BusinessDate = 18 Nov 2026
    /// </summary>
    public DateTime CalculateBusinessDate(DateTime transactionDateTimePkt)
    {
        if (transactionDateTimePkt.TimeOfDay < TimeSpan.FromHours(4))
        {
            return transactionDateTimePkt.Date.AddDays(-1);
        }

        return transactionDateTimePkt.Date;
    }

    public DateTime GetCurrentBusinessDate()
    {
        return CalculateBusinessDate(GetNowPakistanTime());
    }

    /// <summary>
    /// Returns the exact operational window for a single BusinessDate:
    /// Start: BusinessDate at 6:00 PM (18:00:00)
    /// End:   Next Calendar Day at 4:00 AM (04:00:00) exclusive
    /// </summary>
    public (DateTime WindowStart, DateTime WindowEnd) GetBusinessDayWindow(DateTime businessDate)
    {
        var dateOnly = businessDate.Date;
        var windowStart = dateOnly.AddHours(18);           // 6:00 PM on BusinessDate
        var windowEnd = dateOnly.AddDays(1).AddHours(4);   // 4:00 AM Next Day (exclusive)
        return (windowStart, windowEnd);
    }

    public (DateTime WindowStart, DateTime WindowEnd) GetBusinessDateRangeWindow(DateTime startBusinessDate, DateTime endBusinessDate)
    {
        var startWindow = GetBusinessDayWindow(startBusinessDate.Date);
        var endWindow = GetBusinessDayWindow(endBusinessDate.Date);
        return (startWindow.WindowStart, endWindow.WindowEnd);
    }

    public async Task<BusinessDay> EnsureBusinessDayRecordAsync(DateTime businessDate, string? userId = null)
    {
        var targetDate = businessDate.Date;
        var existing = await _db.BusinessDays.FirstOrDefaultAsync(b => b.BusinessDate == targetDate);
        if (existing != null)
        {
            return existing;
        }

        var (windowStart, windowEnd) = GetBusinessDayWindow(targetDate);
        var record = new BusinessDay
        {
            BusinessDate = targetDate,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            IsClosed = false,
            OpenedAt = GetNowPakistanTime(),
            OpenedByUserId = userId
        };

        _db.BusinessDays.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }
}
