namespace SimulationChallenge2026;

public class Shipment
{
    public int Index { get; set; }
    public int TeuSize { get; set; }
    public Vessel? CarryingVessel { get; set; }
    public Demand Demand { get; set; } = null!;
    public Port? CurrentStoragePort { get; set; }
    public List<Booking> AssociatedBookings { get; } = new();
    public int? CurrentBookingIndex { get; set; }

    public override string ToString()
    {
        string status;

        if (CarryingVessel != null)
        {
            status = $"On Vessel {CarryingVessel.Index}";
        }
        else if (CurrentStoragePort != null)
        {
            status = $"At {CurrentStoragePort.Name}";
        }
        else
        {
            status = "In Transit";
        }

        string bookingStage = CurrentBookingIndex.HasValue
            ? $"Booking[{CurrentBookingIndex}]"
            : "NoBooking";

        return $"Shipment {Index} | {TeuSize} TEU | " +
               $"{Demand.OriginPort.Name} -> {Demand.DestinationPort.Name} | " +
               $"{bookingStage} | {status}";
    }

    /// <summary>
    /// Retrieves the current booking of this shipment based on its booking sequence index.
    ///
    /// The current booking is determined by:
    /// - AssociatedBookings: the full sequence of bookings for this shipment
    /// - CurrentBookingIndex: the index indicating the shipment's current position
    ///
    /// This method centralizes the logic for resolving the shipment's current
    /// operational state within its booking chain, and should be used wherever
    /// the current booking is needed.
    ///
    /// </summary>
    /// <returns>
    /// The booking corresponding to the shipment's current position.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if:
    /// - AssociatedBookings is null,
    /// - or no booking matches the CurrentBookingIndex.
    /// </exception>
    public Booking GetCurrentBooking()
    {
        if (AssociatedBookings == null)
        {
            throw new InvalidOperationException(
                $"Shipment {Index} has no associated bookings.");
        }

        var booking = AssociatedBookings
            .FirstOrDefault(b => b.SequenceIndex == CurrentBookingIndex);

        if (booking == null)
        {
            throw new InvalidOperationException(
                $"Shipment {Index} has no booking matching CurrentBookingIndex {CurrentBookingIndex}.");
        }

        return booking;
    }

    /// <summary>
    /// Determines whether the current booking is the last booking
    /// in the shipment's booking sequence.
    ///
    /// The current booking is identified by CurrentBookingIndex.
    /// The last booking is the one with the largest SequenceIndex
    /// among AssociatedBookings.
    /// </summary>
    /// <returns>
    /// True if the current booking is the last one; otherwise false.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if:
    /// - the shipment has no associated bookings,
    /// - or no booking matches the CurrentBookingIndex.
    /// </exception>
    public bool IsAtLastBooking()
    {
        var currentBooking = GetCurrentBooking();
        var lastSequenceIndex = AssociatedBookings.Max(b => b.SequenceIndex);

        return currentBooking.SequenceIndex == lastSequenceIndex;
    }
}
