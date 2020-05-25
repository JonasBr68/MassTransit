﻿namespace MassTransit
{
    using System;
    using ConsumeConfigurators;
    using GreenPipes;
    using Saga;
    using SagaConfigurators;


    public interface ISagaConfigurator<TSaga> :
        IPipeConfigurator<SagaConsumeContext<TSaga>>,
        ISagaConfigurationObserverConnector,
        IConsumeConfigurator
        where TSaga : class, ISaga
    {
        /// <summary>
        /// Add middleware to the message pipeline, which is invoked prior to the saga repository.
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="configure">The callback to configure the message pipeline</param>
        void Message<T>(Action<ISagaMessageConfigurator<T>> configure)
            where T : class;

        /// <summary>
        /// Add middleware to the saga pipeline, for the specified message type, which is invoked
        /// after the saga repository.
        /// </summary>
        /// <typeparam name="T">The message type</typeparam>
        /// <param name="configure">The callback to configure the message pipeline</param>
        void SagaMessage<T>(Action<ISagaMessageConfigurator<TSaga, T>> configure)
            where T : class;
    }
}
