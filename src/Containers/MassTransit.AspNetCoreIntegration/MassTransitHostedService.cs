namespace MassTransit.AspNetCoreIntegration
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Registration;
    using Util;


    public class MassTransitHostedService :
        IHostedService
    {
        readonly IBusRegistry _registry;
        Task _startTask;

        public MassTransitHostedService(IBusRegistry registry)
        {
            _registry = registry;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _startTask = _registry.Start(cancellationToken);

            return _startTask.IsCompleted
                ? _startTask
                : TaskUtil.Completed;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _registry.Stop(cancellationToken);
        }
    }
}
