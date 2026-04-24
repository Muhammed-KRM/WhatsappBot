using CafeBot.Data.Enums;

namespace CafeBot.Business.DTOs;

public record ActivityLogDto(
    int Id,
    DateTime Timestamp,
    ActivityType Type,
    string Message,
    string? Details
);
