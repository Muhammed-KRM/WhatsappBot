// Bağlantı durumu alert'i gösterme
window.showConnectionAlert = (type, message) => {
    const alertElement = document.getElementById('connection-alert');
    const messageElement = document.getElementById('connection-message');
    
    if (alertElement && messageElement) {
        // Alert tipine göre CSS class'ı ayarla
        alertElement.className = `alert alert-${type} alert-dismissible`;
        messageElement.textContent = message;
        alertElement.style.display = 'block';
        
        // 10 saniye sonra otomatik gizle
        setTimeout(() => {
            alertElement.style.display = 'none';
        }, 10000);
    }
};

// Browser notification izni isteme
window.requestNotificationPermission = async () => {
    if ('Notification' in window && Notification.permission === 'default') {
        try {
            await Notification.requestPermission();
        } catch (error) {
            console.log('Notification permission request failed:', error);
        }
    }
};

// Browser notification gösterme
window.showBrowserNotification = (title, body) => {
    if ('Notification' in window && Notification.permission === 'granted') {
        try {
            const notification = new Notification(title, {
                body: body,
                icon: '/favicon.png',
                badge: '/favicon.png',
                tag: 'cafebot-connection',
                requireInteraction: true
            });
            
            // 5 saniye sonra otomatik kapat
            setTimeout(() => {
                notification.close();
            }, 5000);
            
            // Tıklandığında pencereyi odakla
            notification.onclick = () => {
                window.focus();
                notification.close();
            };
        } catch (error) {
            console.log('Browser notification failed:', error);
        }
    }
};

// Sayfa yüklendiğinde notification izni iste
document.addEventListener('DOMContentLoaded', () => {
    window.requestNotificationPermission();
});