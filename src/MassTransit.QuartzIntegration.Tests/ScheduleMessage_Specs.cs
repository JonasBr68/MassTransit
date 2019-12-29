﻿namespace MassTransit.QuartzIntegration.Tests
{
    using System;
    using System.Threading.Tasks;
    using GreenPipes;
    using NUnit.Framework;


    [TestFixture]
    public class ScheduleMessage_Specs :
        QuartzInMemoryTestFixture
    {
        Task<ConsumeContext<SecondMessage>> _second;
        Task<ConsumeContext<FirstMessage>> _first;

        protected override void ConfigureInMemoryReceiveEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            _first = Handler<FirstMessage>(configurator, async context =>
            {
                await context.ScheduleSend(TimeSpan.FromSeconds(10), new SecondMessage());
            });

            _second = Handled<SecondMessage>(configurator);
        }


        public class FirstMessage
        {
        }


        public class SecondMessage
        {
        }


        [Test]
        public async Task Should_get_both_messages()
        {
            await Bus.ScheduleSend(InputQueueAddress, DateTime.Now, new FirstMessage());

            await _first;

            AdvanceTime(TimeSpan.FromSeconds(10));

            await _second;
        }
    }

    [TestFixture]
    public class Specifying_an_expiration_time :
        QuartzInMemoryTestFixture
    {
        Task<ConsumeContext<SecondMessage>> _second;
        Task<ConsumeContext<FirstMessage>> _first;

        protected override void ConfigureInMemoryReceiveEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            _first = Handler<FirstMessage>(configurator, async context =>
            {
                await context.ScheduleSend(TimeSpan.FromSeconds(10), new SecondMessage(),
                    Pipe.Execute<SendContext>(x => x.TimeToLive = TimeSpan.FromSeconds(30)));
            });

            _second = Handled<SecondMessage>(configurator);
        }


        public class FirstMessage
        {
        }


        public class SecondMessage
        {
        }


        [Test]
        public async Task Should_include_it_with_the_final_message()
        {
            await Bus.ScheduleSend(InputQueueAddress, DateTime.Now, new FirstMessage());

            await _first;

            AdvanceTime(TimeSpan.FromSeconds(10));

            var second = await _second;

            Assert.That(second.ExpirationTime.HasValue, Is.True);
            Assert.That(second.ExpirationTime.Value, Is.GreaterThan(DateTime.UtcNow + TimeSpan.FromSeconds(20)));
        }
    }
}
