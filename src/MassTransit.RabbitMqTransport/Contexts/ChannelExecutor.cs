namespace MassTransit.RabbitMqTransport.Contexts
{
    using System;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using GreenPipes;


    public class ChannelExecutor :
        IAsyncDisposable
    {
        readonly Channel<IFuture> _channel;
        readonly Task[] _runTasks;

        public ChannelExecutor(int prefetchCount, int concurrencyLimit)
        {
            if (prefetchCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(prefetchCount));
            if (concurrencyLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLimit));

            var channelOptions = new BoundedChannelOptions(prefetchCount)
            {
                AllowSynchronousContinuations = true,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = concurrencyLimit == 1,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<IFuture>(channelOptions);

            _runTasks = new Task[concurrencyLimit];

            for (var i = 0; i < concurrencyLimit; i++)
                _runTasks[i] = Task.Run(RunFromChannel);
        }

        public ChannelExecutor(int concurrencyLimit)
        {
            var channelOptions = new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = concurrencyLimit == 1,
                SingleWriter = false
            };

            _channel = Channel.CreateUnbounded<IFuture>(channelOptions);

            _runTasks = new Task[concurrencyLimit];

            for (var i = 0; i < concurrencyLimit; i++)
                _runTasks[i] = Task.Run(RunFromChannel);
        }

        public Task DisposeAsync(CancellationToken cancellationToken)
        {
            _channel.Writer.Complete();

            return Task.WhenAll(_runTasks);
        }

        public Task Run(Func<Task> method, CancellationToken cancellationToken = default)
        {
            async Task<bool> RunMethod()
            {
                await method().ConfigureAwait(false);

                return true;
            }

            return Run(RunMethod, cancellationToken);
        }

        public async Task<T> Run<T>(Func<Task<T>> method, CancellationToken cancellationToken = default)
        {
            var future = new Future<T>(method, cancellationToken);

            await _channel.Writer.WriteAsync(future, cancellationToken).ConfigureAwait(false);

            return await future.Completed.ConfigureAwait(false);
        }

        public Task Run(Action method, CancellationToken cancellationToken = default)
        {
            bool RunMethod()
            {
                method();

                return true;
            }

            return Run(RunMethod, cancellationToken);
        }

        public async Task<T> Run<T>(Func<T> method, CancellationToken cancellationToken = default)
        {
            var future = new SynchronousFuture<T>(method, cancellationToken);

            await _channel.Writer.WriteAsync(future, cancellationToken).ConfigureAwait(false);

            return await future.Completed.ConfigureAwait(false);
        }

        async Task RunFromChannel()
        {
            while (await _channel.Reader.WaitToReadAsync())
            {
                if (_channel.Reader.TryRead(out var future))
                {
                    var task = future.Run();
                    if (task.IsCompleted)
                        continue;

                    await task.ConfigureAwait(false);
                }
            }
        }


        interface IFuture
        {
            Task Run();
        }


        readonly struct Future<T> :
            IFuture
        {
            readonly Func<Task<T>> _method;
            readonly CancellationToken _cancellationToken;
            readonly TaskCompletionSource<T> _completion;

            public Future(Func<Task<T>> method, CancellationToken cancellationToken)
            {
                _method = method;
                _cancellationToken = cancellationToken;
                _completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            /// <summary>
            /// The post-execution result, which can be awaited
            /// </summary>
            public Task<T> Completed => _completion.Task;

            public async Task Run()
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    _completion.TrySetCanceled();
                    return;
                }

                try
                {
                    var result = await _method().ConfigureAwait(false);

                    _completion.TrySetResult(result);
                }
                catch (OperationCanceledException exception) when (exception.CancellationToken == _cancellationToken)
                {
                    _completion.TrySetCanceled();
                }
                catch (Exception exception)
                {
                    _completion.TrySetException(exception);
                }
            }
        }


        readonly struct SynchronousFuture<T> :
            IFuture
        {
            readonly Func<T> _method;
            readonly CancellationToken _cancellationToken;
            readonly TaskCompletionSource<T> _completion;

            public SynchronousFuture(Func<T> method, CancellationToken cancellationToken)
            {
                _method = method;
                _cancellationToken = cancellationToken;
                _completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            /// <summary>
            /// The post-execution result, which can be awaited
            /// </summary>
            public Task<T> Completed => _completion.Task;

            public Task Run()
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    _completion.TrySetCanceled();

                    return Task.CompletedTask;
                }

                try
                {
                    var result = _method();

                    _completion.TrySetResult(result);
                }
                catch (OperationCanceledException exception) when (exception.CancellationToken == _cancellationToken)
                {
                    _completion.TrySetCanceled();
                }
                catch (Exception exception)
                {
                    _completion.TrySetException(exception);
                }

                return Task.CompletedTask;
            }
        }
    }
}
