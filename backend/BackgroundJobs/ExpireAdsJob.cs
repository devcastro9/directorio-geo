using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace backend.BackgroundJobs
{
    public class ExpireAdsJob : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
