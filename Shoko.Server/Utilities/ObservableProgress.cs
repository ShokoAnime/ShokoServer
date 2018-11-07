using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Shoko.Server.Utilities
{
    /// <summary>
    /// An <see cref="IProgress{T}"/> that is disposable.
    /// </summary>
    /// <typeparam name="T">The type of progress updates.</typeparam>
    public interface IDisposableProgress<in T> : IProgress<T>, IDisposable
    {
    }

    /// <summary>
    /// A progress reporter that exposes progress updates as an observable stream. This is a hot observable.
    /// </summary>
    /// <typeparam name="T">The type of progress updates.</typeparam>
    public class ObservableProgress<T> : IObservable<T>, IDisposableProgress<T>
    {
        private readonly ISubject<T> _subject;

        /// <summary>
        /// Creates an observable progress that uses a replay subject with a single-element buffer, ensuring all new subscriptions immediately receive the last progress update.
        /// </summary>
        public ObservableProgress()
            : this(new ReplaySubject<T>(1))
        {
        }

        /// <summary>
        /// Creates an observable progress that uses a replay subject with a single-element buffer, ensuring all new subscriptions immediately receive the last progress update.
        /// </summary>
        /// <param name="scheduler">The scheduler to inject into the replay subject.</param>
        public ObservableProgress(IScheduler scheduler)
            : this(new ReplaySubject<T>(1, scheduler))
        {
        }

        /// <summary>
        /// Creates an observable progress that uses the specified subject.
        /// </summary>
        /// <param name="subject">The subject used for progress updates.</param>
        public ObservableProgress(ISubject<T> subject)
        {
            _subject = subject;
        }

        void IProgress<T>.Report(T value)
        {
            _subject.OnNext(value);
        }

        public void Dispose()
        {
            _subject.OnCompleted();
            var disposableSubject = _subject as IDisposable;
            if (disposableSubject != null)
                disposableSubject.Dispose();
        }

        IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
        {
            return _subject.Subscribe(observer);
        }

        /// <summary>
        /// Creates a progress handler with common UI options: updates are sampled on <paramref name="sampleTimeSpan"/> intervals, and the <paramref name="handler"/> is executed on the UI thread. This method must be called from the UI thread. The UI should already be initialized with the default state; <paramref name="handler"/> is not invoked with an initial value.
        /// </summary>
        /// <param name="sampleTimeSpan">The time span interval to sample progress updates.</param>
        /// <param name="handler">The progress update handler that updates the UI.</param>
        public static IDisposableProgress<T> CreateForUi(TimeSpan sampleTimeSpan, Action<T> handler)
        {
            var uiScheduler = SynchronizationContext.Current ?? new SynchronizationContext();
            var progress = new ObservableProgress<T>(new Subject<T>());
            var subscription = progress.Sample(sampleTimeSpan).ObserveOn(uiScheduler).Subscribe(handler);
            return new ObservableProgressWithSubscription(progress, subscription);
        }

        /// <summary>
        /// Creates a progress handler with common UI options: updates are sampled on <paramref name="sampleTimeSpan"/> intervals, and the <paramref name="handler"/> is executed on the UI thread. This method must be called from the UI thread. The UI should already be initialized with the default state; <paramref name="handler"/> is not invoked with an initial value.
        /// </summary>
        /// <param name="sampleTimeSpan">The time span interval to sample progress updates.</param>
        /// <param name="handler">The progress update handler that updates the UI.</param>
        /// <param name="scheduler">The scheduler to inject into the <c>Sample</c> operator.</param>
        public static IDisposableProgress<T> CreateForUi(TimeSpan sampleTimeSpan, Action<T> handler, IScheduler scheduler)
        {
            var uiScheduler = SynchronizationContext.Current ?? new SynchronizationContext();
            var progress = new ObservableProgress<T>(new Subject<T>());
            var subscription = progress.Sample(sampleTimeSpan, scheduler).ObserveOn(uiScheduler).Subscribe(handler);
            return new ObservableProgressWithSubscription(progress, subscription);
        }

        /// <summary>
        /// Creates a progress handler with common UI options: updates are sampled on 100ms intervals, and the <paramref name="handler"/> is executed on the UI thread. This method must be called from the UI thread. The UI should already be initialized with the default state; <paramref name="handler"/> is not invoked with an initial value.
        /// </summary>
        /// <param name="handler">The progress update handler that updates the UI.</param>
        public static IDisposableProgress<T> CreateForUi(Action<T> handler)
        {
            return CreateForUi(TimeSpan.FromMilliseconds(100), handler);
        }

        /// <summary>
        /// Creates a progress handler with common UI options: updates are sampled on 100ms intervals, and the <paramref name="handler"/> is executed on the UI thread. This method must be called from the UI thread. The UI should already be initialized with the default state; <paramref name="handler"/> is not invoked with an initial value.
        /// </summary>
        /// <param name="handler">The progress update handler that updates the UI.</param>
        /// <param name="scheduler">The scheduler to inject into the <c>Sample</c> operator.</param>
        public static IDisposableProgress<T> CreateForUi(Action<T> handler, IScheduler scheduler)
        {
            return CreateForUi(TimeSpan.FromMilliseconds(100), handler, scheduler);
        }

        private sealed class ObservableProgressWithSubscription : IDisposableProgress<T>
        {
            private readonly IDisposableProgress<T> _progress;
            private readonly IDisposable _subscription;

            public ObservableProgressWithSubscription(ObservableProgress<T> progress, IDisposable subscription)
            {
                _progress = progress;
                _subscription = subscription;
            }

            public void Report(T value)
            {
                _progress.Report(value);
            }

            public void Dispose()
            {
                _subscription.Dispose();
                _progress.Dispose();
            }
        }
    }
}