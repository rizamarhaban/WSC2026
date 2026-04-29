using O2DESNet;

namespace SimulationChallenge2026;

/// <summary>
/// Represents shipments waiting for loading at a transshipment port.
///
/// Design:
/// - Start has no additional gate control.
/// - Finish has no external gate control.
///
/// Before finishing:
/// 1. Adjust the remaining booking suffix, if needed.
/// 2. Advance CurrentBookingIndex to the next booking.
/// 3. Finish the shipment.
/// </summary>
public class Shipment_WaitingForLoadingAtTransshipmentPort : ActivityHandler<Shipment>
{
    public Shipment_WaitingForLoadingAtTransshipmentPort(int seed = 0)
        : base(
              id: nameof(Shipment_WaitingForLoadingAtTransshipmentPort),
              seed: seed)
    {
    }

    protected override void AttemptFinish()
    {
        Log(nameof(AttemptFinish));

        foreach (var shipment in D_LoadsReadyFinish.ToList())
        {
            if (shipment == null)
                throw new ArgumentNullException(nameof(shipment));

            Require(
                shipment.AssociatedBookings.Count > 0,
                nameof(AttemptFinish),
                $"Shipment {shipment.Index} has no associated bookings.");

            Require(
                shipment.CurrentBookingIndex.HasValue,
                nameof(AttemptFinish),
                $"Shipment {shipment.Index} has null CurrentBookingIndex.");

            int currentBookingIndex = shipment.CurrentBookingIndex.Value;

            var currentBooking = shipment.AssociatedBookings
                .FirstOrDefault(booking => booking.SequenceIndex == currentBookingIndex);

            currentBooking = RequireNotNull(
                currentBooking,
                nameof(AttemptFinish),
                $"Shipment {shipment.Index} booking with sequence index {currentBookingIndex}");

            // Step 1: Adjust remaining bookings, if dynamic replanning is enabled later.
            AdjustRemainingBookings(shipment, currentBooking);

            // Step 2: Advance to the next booking in the booking chain.
            var nextBooking = shipment.AssociatedBookings
                .Where(booking => booking.SequenceIndex > currentBookingIndex)
                .OrderBy(booking => booking.SequenceIndex)
                .FirstOrDefault();

            nextBooking = RequireNotNull(
                nextBooking,
                nameof(AttemptFinish),
                $"Shipment {shipment.Index} next booking after sequence index {currentBookingIndex}");

            shipment.CurrentBookingIndex = nextBooking.SequenceIndex;

            // Step 3: Finish this waiting activity.
            Finish(shipment);
        }
    }

    /// <summary>
    /// Placeholder for future dynamic replanning logic.
    ///
    /// Principles:
    /// - Do not modify completed bookings.
    /// - Only adjust the remaining booking suffix.
    /// - Ensure continuity from the current transshipment port to the final destination.
    /// </summary>
    private void AdjustRemainingBookings(Shipment shipment, Booking currentBooking)
    {
        // TODO:
        // Future dynamic routing / replanning logic:
        //
        // Possible extensions:
        // - recompute shortest path from current port to destination;
        // - replace only the suffix after currentBooking;
        // - preserve completed bookings unchanged;
        // - incorporate congestion, delay, or stochastic effects.
    }

    #region Statistics - TEU by Transshipment Port

    /// <summary>
    /// Time-weighted TEU counters grouped by transshipment port.
    ///
    /// Each counter tracks the total TEU volume of shipments currently waiting
    /// for loading at the corresponding transshipment port.
    ///
    /// The counter increases when a shipment starts this activity and decreases
    /// when the shipment departs from this activity.
    /// </summary>
    public Dictionary<Port, HourCounter> HC_TeusByTransshipmentPort { get; } = new();

    protected override void Start(Shipment shipment)
    {
        base.Start(shipment);

        var transshipmentPort = GetArrivalTransshipmentPort(shipment);

        if (!HC_TeusByTransshipmentPort.TryGetValue(transshipmentPort, out var portCounter))
        {
            portCounter = AddHourCounter();
            HC_TeusByTransshipmentPort[transshipmentPort] = portCounter;
        }

        portCounter.ObserveChange(shipment.TeuSize);
    }

    public override void Depart(Shipment shipment)
    {
        if (F_LoadsFinished.Contains(shipment))
        {
            base.Depart(shipment);
            var transshipmentPort = GetDepartureTransshipmentPort(shipment);

            if (!HC_TeusByTransshipmentPort.TryGetValue(transshipmentPort, out var portCounter))
            {
                ThrowActivityException(
                    nameof(Depart),
                    $"Cannot update transshipment TEU statistics for Shipment {shipment.Index} " +
                    $"because no HourCounter exists for transshipment port {transshipmentPort.Name}.");
            }

            portCounter.ObserveChange(-shipment.TeuSize);
        }
    }

    private Port GetArrivalTransshipmentPort(Shipment shipment)
    {
        Require(
            shipment.CurrentBookingIndex.HasValue,
            nameof(GetArrivalTransshipmentPort),
            $"Shipment {shipment.Index} has null CurrentBookingIndex.");

        int currentBookingIndex = shipment.CurrentBookingIndex.Value;

        var currentBooking = shipment.AssociatedBookings
            .FirstOrDefault(booking => booking.SequenceIndex == currentBookingIndex);

        currentBooking = RequireNotNull(
            currentBooking,
            nameof(GetArrivalTransshipmentPort),
            $"Shipment {shipment.Index} booking with sequence index {currentBookingIndex}");

        var serviceRoute = RequireNotNull(
            currentBooking.ServiceRoute,
            nameof(GetArrivalTransshipmentPort),
            $"Shipment {shipment.Index} current booking service route");

        var arrivalSegment = serviceRoute.Segments
            .FirstOrDefault(segment =>
                segment.SequenceIndex == currentBooking.ArrivalSegmentIndex);

        arrivalSegment = RequireNotNull(
            arrivalSegment,
            nameof(GetArrivalTransshipmentPort),
            $"Shipment {shipment.Index} arrival segment {currentBooking.ArrivalSegmentIndex} " +
            $"on service route {serviceRoute.Id}");

        var associatedLeg = RequireNotNull(
            arrivalSegment.AssociatedLeg,
            nameof(GetArrivalTransshipmentPort),
            $"Shipment {shipment.Index} arrival segment {arrivalSegment.SequenceIndex} associated leg");

        return RequireNotNull(
            associatedLeg.ArrivalPort,
            nameof(GetArrivalTransshipmentPort),
            $"Shipment {shipment.Index} arrival transshipment port");
    }

    private Port GetDepartureTransshipmentPort(Shipment shipment)
    {
        Require(
            shipment.CurrentBookingIndex.HasValue,
            nameof(GetDepartureTransshipmentPort),
            $"Shipment {shipment.Index} has null CurrentBookingIndex.");

        int currentBookingIndex = shipment.CurrentBookingIndex.Value;

        var currentBooking = shipment.AssociatedBookings
            .FirstOrDefault(booking => booking.SequenceIndex == currentBookingIndex);

        currentBooking = RequireNotNull(
            currentBooking,
            nameof(GetDepartureTransshipmentPort),
            $"Shipment {shipment.Index} booking with sequence index {currentBookingIndex}");

        var serviceRoute = RequireNotNull(
            currentBooking.ServiceRoute,
            nameof(GetDepartureTransshipmentPort),
            $"Shipment {shipment.Index} current booking service route");

        var departureSegment = serviceRoute.Segments
            .FirstOrDefault(segment =>
                segment.SequenceIndex == currentBooking.DepartureSegmentIndex);

        departureSegment = RequireNotNull(
            departureSegment,
            nameof(GetDepartureTransshipmentPort),
            $"Shipment {shipment.Index} departure segment {currentBooking.DepartureSegmentIndex} " +
            $"on service route {serviceRoute.Id}");

        var associatedLeg = RequireNotNull(
            departureSegment.AssociatedLeg,
            nameof(GetDepartureTransshipmentPort),
            $"Shipment {shipment.Index} departure segment {departureSegment.SequenceIndex} associated leg");

        return RequireNotNull(
            associatedLeg.DeparturePort,
            nameof(GetDepartureTransshipmentPort),
            $"Shipment {shipment.Index} departure transshipment port");
    }

    #endregion
}