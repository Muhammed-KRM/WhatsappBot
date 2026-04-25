using System;
using System.Threading.Tasks;

namespace CafeBot.Business.Interfaces;

public interface INotificationService
{
    Task SendConnectionLostAlertAsync(string sessionName, DateTime disconnectTime, string userEmail);
}
