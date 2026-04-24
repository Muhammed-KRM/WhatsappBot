using CafeBot.Business.DTOs;

namespace CafeBot.Business.Interfaces;

public interface IMessageParserService
{
    Task<List<ShiftSlotDto>?> ParseShiftMessageAsync(string messageText);
    bool IsShiftMessage(string messageText);
}
