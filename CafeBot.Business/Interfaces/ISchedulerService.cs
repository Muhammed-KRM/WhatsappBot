using CafeBot.Business.DTOs;

namespace CafeBot.Business.Interfaces;

public interface ISchedulerService
{
    Task<int?> SelectBestHourAsync(List<ShiftSlotDto> availableSlots, List<int> priorityList);
    Task<bool> IsMessageProcessedAsync(string messageId);
    Task MarkMessageAsProcessedAsync(string messageId, int selectedHour);
}
