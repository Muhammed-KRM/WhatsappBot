using CafeBot.Business.DTOs;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Entities;
using CafeBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace CafeBot.Business.Services;

public class SchedulerManager : ISchedulerService
{
    private readonly IProcessedMessageRepository _processedMessageRepository;
    private readonly ILogService _logService;
    private readonly ILogger<SchedulerManager> _logger;

    public SchedulerManager(
        IProcessedMessageRepository processedMessageRepository,
        ILogService logService,
        ILogger<SchedulerManager> logger)
    {
        _processedMessageRepository = processedMessageRepository ?? throw new ArgumentNullException(nameof(processedMessageRepository));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Selects the best hour from available slots based on priority list
    /// Returns the first matching hour from the priority list that exists in available slots
    /// </summary>
    /// <param name="availableSlots">List of available shift slots</param>
    /// <param name="priorityList">Priority list of hours (ordered by preference)</param>
    /// <returns>Selected hour or null if no match found</returns>
    public Task<int?> SelectBestHourAsync(List<ShiftSlotDto> availableSlots, List<int> priorityList)
    {
        if (availableSlots == null || availableSlots.Count == 0)
            return Task.FromResult<int?>(null);

        if (priorityList == null || priorityList.Count == 0)
            return Task.FromResult<int?>(null);

        // Create a set of available hours for O(1) lookup
        var availableHours = new HashSet<int>(availableSlots.Select(slot => slot.Hour));

        // Iterate through priority list and return first matching hour
        foreach (var priorityHour in priorityList)
        {
            if (availableHours.Contains(priorityHour))
            {
                return Task.FromResult<int?>(priorityHour);
            }
        }

        // No match found
        return Task.FromResult<int?>(null);
    }

    /// <summary>
    /// Checks if a message has already been processed
    /// </summary>
    /// <param name="messageId">The WhatsApp message ID</param>
    /// <returns>True if the message has been processed</returns>
    public async Task<bool> IsMessageProcessedAsync(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return false;

        try
        {
            return await _processedMessageRepository.IsMessageProcessedAsync(messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj işlenme durumu kontrol edilirken hata oluştu. MessageId: {MessageId}", messageId);
            await _logService.LogFunctionErrorAsync("SCHEDULER_CHECK_ERROR", ex, new { messageId });
            return false;
        }
    }

    /// <summary>
    /// Marks a message as processed and stores the selected hour
    /// </summary>
    /// <param name="messageId">The WhatsApp message ID</param>
    /// <param name="selectedHour">The hour that was selected</param>
    public async Task MarkMessageAsProcessedAsync(string messageId, int selectedHour)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

        if (selectedHour < 0 || selectedHour > 23)
            throw new ArgumentOutOfRangeException(nameof(selectedHour), "Hour must be between 0 and 23");

        try
        {
            // Check if message is already processed
            var existingMessage = await _processedMessageRepository.GetByMessageIdAsync(messageId);
            if (existingMessage != null)
            {
                _logger.LogDebug("Mesaj zaten işlenmiş olarak işaretli. MessageId: {MessageId}", messageId);
                return;
            }

            // Create new processed message entity
            var processedMessage = new ProcessedMessage
            {
                MessageId = messageId,
                ProcessedAt = DateTime.UtcNow,
                SelectedHour = selectedHour.ToString()
            };

            await _processedMessageRepository.AddAsync(processedMessage);
            await _processedMessageRepository.SaveChangesAsync();
            _logger.LogInformation("Mesaj işlendi olarak işaretlendi. MessageId: {MessageId}, SelectedHour: {Hour}", messageId, selectedHour);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj işlendi olarak işaretlenirken hata oluştu. MessageId: {MessageId}", messageId);
            await _logService.LogFunctionErrorAsync("SCHEDULER_MARK_ERROR", ex, new { messageId, selectedHour });
            throw;
        }
    }
}
