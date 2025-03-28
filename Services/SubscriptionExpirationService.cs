using Microsoft.EntityFrameworkCore;
using Streamflix.Data;
using Streamflix.Model;

namespace Streamflix.Services
{
    public class SubscriptionExpirationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionExpirationService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public SubscriptionExpirationService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionExpirationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var now = DateTime.UtcNow;

                        var subscriptionsToExpire = await dbContext.UserSubscription
                            .Where(us => us.Status == Model.SubscriptionStatus.Ongoing && us.EndDate <= now)
                            .ToListAsync(stoppingToken);

                        if (subscriptionsToExpire.Any())
                        {
                            foreach (var subscription in subscriptionsToExpire)
                            {
                                subscription.Status = SubscriptionStatus.Expired;
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating expired subscriptions.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
