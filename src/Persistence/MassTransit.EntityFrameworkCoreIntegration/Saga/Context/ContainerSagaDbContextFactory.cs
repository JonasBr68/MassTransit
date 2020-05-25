namespace MassTransit.EntityFrameworkCoreIntegration.Saga.Context
{
    using System.Threading.Tasks;
    using MassTransit.Saga;
    using Microsoft.EntityFrameworkCore;
    using Util;


    public class ContainerSagaDbContextFactory<TContext, TSaga> :
        ISagaDbContextFactory<TSaga>
        where TContext : DbContext
        where TSaga : class, ISaga
    {
        readonly TContext _dbContext;

        public ContainerSagaDbContextFactory(TContext dbContext)
        {
            _dbContext = dbContext;
        }

        public DbContext Create()
        {
            return _dbContext;
        }

        public DbContext CreateScoped<T>(ConsumeContext<T> context)
            where T : class
        {
            return _dbContext;
        }

        public Task ReleaseAsync(DbContext dbContext)
        {
            return TaskUtil.Completed;
        }
    }
}