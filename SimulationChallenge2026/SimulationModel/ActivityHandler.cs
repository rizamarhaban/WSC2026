using O2DESNet;

namespace SimulationChallenge2026;

/// <summary>
/// Abstract base class for activity processing in the discrete-event simulation.
///
/// This class defines a standard activity lifecycle:
///
/// RequestStart -> Start -> ReadyFinish -> Finish -> Depart
///
/// State sets:
/// - R: loads that have requested start.
/// - S: loads currently being processed.
/// - D: loads that have completed their duration and are ready to finish.
/// - F: loads that have finished this activity but have not yet departed.
///
/// Default behaviour:
/// - Start is unconditional once requested.
/// - Finish is unconditional once ready.
/// - Duration is zero unless T_Duration is specified.
///
/// Derived activity handlers may override:
/// - AttemptStart() for start-side gate logic.
/// - AttemptFinish() for finish-side gate logic.
/// - T_Duration for load-specific processing time.
/// </summary>
public abstract class ActivityHandler<TLoad> : Sandbox
{
    /// <summary>
    /// Loads that have requested to start this activity.
    /// </summary>
    public HashSet<TLoad> R_LoadsRequestedStart { get; } = new();

    /// <summary>
    /// Loads currently being processed by this activity.
    /// </summary>
    public HashSet<TLoad> S_LoadsStarted { get; } = new();

    /// <summary>
    /// Loads whose processing duration has elapsed and are ready to finish.
    /// </summary>
    public HashSet<TLoad> D_LoadsReadyFinish { get; } = new();

    /// <summary>
    /// Loads that have finished this activity but have not yet departed
    /// from this activity node.
    /// </summary>
    public HashSet<TLoad> F_LoadsFinished { get; } = new();

    /// <summary>
    /// Whether activity lifecycle events should be printed to the console.
    /// </summary>
    public bool EnableLog { get; set; } = true;

    /// <summary>
    /// Duration function of this activity.
    ///
    /// If null, the activity duration defaults to zero.
    /// The Random argument is supplied by the activity's default random stream.
    /// </summary>
    public Func<TLoad, Random, TimeSpan>? T_Duration { get; protected set; } = null;

    /// <summary>
    /// Time-weighted counter for the number of loads in R.
    /// </summary>
    public HourCounter R_HourCounter { get; }

    /// <summary>
    /// Time-weighted counter for the number of loads in S.
    /// </summary>
    public HourCounter S_HourCounter { get; }

    /// <summary>
    /// Time-weighted counter for the number of loads in D.
    /// </summary>
    public HourCounter D_HourCounter { get; }

    /// <summary>
    /// Time-weighted counter for the number of loads in F.
    /// </summary>
    public HourCounter F_HourCounter { get; }

    /// <summary>
    /// Actions triggered immediately after a load starts this activity.
    ///
    /// These callbacks are commonly used to trigger departure from an upstream
    /// activity or to send start signals to related activities.
    /// </summary>
    public List<Action<TLoad>> OnStart { get; } = new();

    /// <summary>
    /// Actions triggered immediately after a load finishes this activity.
    ///
    /// These callbacks are commonly used to request start in downstream
    /// activities or to terminate a load from the process flow.
    /// </summary>
    public List<Action<TLoad>> OnFinish { get; } = new();

    protected ActivityHandler(string id, int seed = 0)
        : base(id: id, seed: seed)
    {
        R_HourCounter = AddHourCounter();
        S_HourCounter = AddHourCounter();
        D_HourCounter = AddHourCounter();
        F_HourCounter = AddHourCounter();
    }

    /// <summary>
    /// External request for a load to start this activity.
    ///
    /// The load is added to R and AttemptStart is scheduled.
    /// Actual start may occur immediately or later, depending on the
    /// derived activity's AttemptStart logic.
    /// </summary>
    public virtual void RequestStart(TLoad load)
    {
        Log(nameof(RequestStart), load);

        if (R_LoadsRequestedStart.Add(load))
        {
            R_HourCounter.ObserveChange(1);
            Schedule(AttemptStart);
        }
    }

    /// <summary>
    /// Attempts to start requested loads.
    ///
    /// Default logic starts all loads currently in R.
    /// Derived classes may override this method to implement start-side
    /// conditions, such as capacity, matching, resource availability,
    /// or external signal requirements.
    /// </summary>
    protected virtual void AttemptStart()
    {
        Log(nameof(AttemptStart));

        foreach (var load in R_LoadsRequestedStart.ToList())
        {
            Start(load);
        }
    }

    /// <summary>
    /// Starts processing a load.
    ///
    /// State transition:
    /// R -> S
    ///
    /// After the load enters S, this method computes the activity duration
    /// and schedules ReadyFinish(load).
    /// </summary>
    protected virtual void Start(TLoad load)
    {
        Log(nameof(Start), load);

        if (!R_LoadsRequestedStart.Remove(load))
            return;

        R_HourCounter.ObserveChange(-1);

        if (S_LoadsStarted.Add(load))
            S_HourCounter.ObserveChange(1);

        var duration = T_Duration?.Invoke(load, DefaultRS) ?? TimeSpan.Zero;

        Schedule(() => ReadyFinish(load), duration);

        foreach (var action in OnStart)
        {
            action?.Invoke(load);
        }
    }

    /// <summary>
    /// Marks a started load as ready to finish.
    ///
    /// State transition:
    /// S -> D
    ///
    /// After the load enters D, AttemptFinish is scheduled. The actual finish
    /// may occur immediately or later, depending on the derived activity's
    /// AttemptFinish logic.
    /// </summary>
    protected virtual void ReadyFinish(TLoad load)
    {
        Log(nameof(ReadyFinish), load);

        if (!S_LoadsStarted.Remove(load))
            return;

        S_HourCounter.ObserveChange(-1);

        if (D_LoadsReadyFinish.Add(load))
            D_HourCounter.ObserveChange(1);

        Schedule(AttemptFinish);
    }

    /// <summary>
    /// Attempts to finish ready loads.
    ///
    /// Default logic finishes all loads currently in D.
    /// Derived classes may override this method to implement finish-side
    /// conditions, such as resource release, matching, downstream readiness,
    /// or external signal requirements.
    /// </summary>
    protected virtual void AttemptFinish()
    {
        Log(nameof(AttemptFinish));

        foreach (var load in D_LoadsReadyFinish.ToList())
        {
            Finish(load);
        }
    }

    /// <summary>
    /// Finishes a ready load.
    ///
    /// State transition:
    /// D -> F
    ///
    /// The load remains in F until Depart(load) is called. This supports
    /// activity-to-activity transfer semantics, where a load only departs
    /// from the current activity after the next activity has actually started it.
    /// </summary>
    protected virtual void Finish(TLoad load)
    {
        Log(nameof(Finish), load);

        if (!D_LoadsReadyFinish.Remove(load))
            return;

        D_HourCounter.ObserveChange(-1);

        if (F_LoadsFinished.Add(load))
            F_HourCounter.ObserveChange(1);

        foreach (var action in OnFinish)
        {
            action?.Invoke(load);
        }
    }

    /// <summary>
    /// Removes a finished load from this activity.
    ///
    /// State transition:
    /// F -> exit
    ///
    /// This is normally triggered either by a downstream activity starting
    /// the same load, or by a terminal condition.
    /// </summary>
    public virtual void Depart(TLoad load)
    {
        Log(nameof(Depart), load);

        if (!F_LoadsFinished.Remove(load))
            return;

        F_HourCounter.ObserveChange(-1);

        // A departure may free capacity or unlock conditions for other loads.
        Schedule(AttemptStart);
    }

    /// <summary>
    /// Writes a standardized activity log message.
    /// </summary>
    protected void Log(string eventName, object? load = null)
    {
        if (!EnableLog) return;

        var time = $"[{ClockTime:yyyy-MM-dd HH:mm:ss}]";

        if (load == null)
            Console.WriteLine($"{time} {Id} | {eventName}");
        else
            Console.WriteLine($"{time} {Id} | {eventName} | {load}");
    }

    /// <summary>
    /// Creates a standardized InvalidOperationException for this activity.
    ///
    /// The generated message includes:
    /// - simulation clock time;
    /// - activity id;
    /// - event or method name;
    /// - detailed error message.
    /// </summary>
    protected InvalidOperationException ActivityException(
        string eventName,
        string message)
    {
        return new InvalidOperationException(
            $"[{ClockTime:yyyy-MM-dd HH:mm:ss}] {Id} | {eventName} | {message}");
    }

    /// <summary>
    /// Throws a standardized InvalidOperationException for this activity.
    ///
    /// This is useful when the caller does not need to use the exception object
    /// directly and simply wants to fail with a formatted activity error.
    /// </summary>
    protected void ThrowActivityException(
        string eventName,
        string message)
    {
        throw ActivityException(eventName, message);
    }

    /// <summary>
    /// Returns the value if it is not null; otherwise throws a standardized
    /// activity exception.
    ///
    /// Example:
    /// var route = RequireNotNull(
    ///     vessel.AssignedServiceRoute,
    ///     nameof(AttemptStart),
    ///     $"Vessel {vessel.Index} assigned service route");
    /// </summary>
    protected TValue RequireNotNull<TValue>(
        TValue? value,
        string eventName,
        string description)
        where TValue : class
    {
        if (value != null)
            return value;

        throw ActivityException(eventName, $"{description} is null.");
    }

    /// <summary>
    /// Validates a condition and throws a standardized activity exception
    /// if the condition is false.
    ///
    /// Example:
    /// Require(
    ///     remainingCapacity >= 0,
    ///     nameof(AttemptStart),
    ///     $"Vessel {vessel.Index} exceeds TEU capacity.");
    /// </summary>
    protected void Require(
        bool condition,
        string eventName,
        string message)
    {
        if (!condition)
        {
            throw ActivityException(eventName, message);
        }
    }

    /// <summary>
    /// Connects this activity to the next activity in the process flow.
    ///
    /// Flow semantics:
    /// 1. When a load finishes this activity, it requests start in the next activity.
    /// 2. When the same load starts in the next activity, it departs from this activity.
    ///
    /// This represents a standard activity-to-activity transfer. The load is
    /// held in F of the current activity until the next activity has actually
    /// started it.
    ///
    /// Optional filter:
    /// - If provided, only loads satisfying the filter are forwarded to the
    ///   next activity.
    /// - This is useful for routing logic, such as destination-based,
    ///   route-based, type-based, or status-based transitions.
    /// </summary>
    public void ConnectTo(
        ActivityHandler<TLoad> next,
        Func<TLoad, bool>? filter = null)
    {
        if (next == null)
            throw new ArgumentNullException(nameof(next));

        // Forward flow: this.Finish -> next.RequestStart.
        this.OnFinish.Add(load =>
        {
            if (filter == null || filter(load))
            {
                next.RequestStart(load);
            }
        });

        // Departure trigger: next.Start -> this.Depart.
        next.OnStart.Add(load =>
        {
            this.Depart(load);
        });
    }

    /// <summary>
    /// Terminates loads at this activity after they finish.
    ///
    /// Flow semantics:
    /// 1. When a load finishes this activity, it is checked against the
    ///    optional filter.
    /// 2. If the filter is satisfied, or no filter is provided, the load
    ///    immediately departs from this activity.
    ///
    /// This is useful for terminal nodes or end-of-flow conditions, where
    /// finished loads should leave the model directly instead of being
    /// forwarded to another downstream activity.
    /// </summary>
    public void Terminate(Func<TLoad, bool>? filter = null)
    {
        this.OnFinish.Add(load =>
        {
            if (filter == null || filter(load))
            {
                this.Depart(load);
            }
        });
    }
}