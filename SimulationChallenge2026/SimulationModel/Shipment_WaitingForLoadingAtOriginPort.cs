using O2DESNet;

namespace SimulationChallenge2026;

/// <summary>
/// Represents shipments waiting for loading at their origin port.
///
/// This activity is responsible for preparing a shipment before it enters
/// the transport process. In particular, before a shipment leaves this
/// waiting state, the activity assigns a chain of bookings that connects
/// the shipment's origin port to its destination port.
///
/// Design:
/// - Start has no additional gate control.
/// - Finish has no external gate control.
/// - Before finishing, each ready shipment is assigned a feasible booking chain.
/// </summary>
public class Shipment_WaitingForLoadingAtOriginPort : ActivityHandler<Shipment>
{
    /// <summary>
    /// Maritime scenario data used to construct feasible booking options.
    /// </summary>
    public MaritimeDataContext MaritimeDataContext { get; }

    public Shipment_WaitingForLoadingAtOriginPort(
        MaritimeDataContext maritimeDataContext,
        int seed = 0)
        : base(
              id: nameof(Shipment_WaitingForLoadingAtOriginPort),
              seed: seed)
    {
        MaritimeDataContext = maritimeDataContext
            ?? throw new ArgumentNullException(nameof(maritimeDataContext));
    }

    /// <summary>
    /// Attempts to finish all ready shipments.
    ///
    /// Since this activity has no external finish gate, every ready shipment
    /// can finish immediately. Before finishing, the shipment is assigned
    /// its booking chain.
    /// </summary>
    protected override void AttemptFinish()
    {
        Log(nameof(AttemptFinish));

        foreach (var shipment in D_LoadsReadyFinish.ToList())
        {
            AssignAssociatedBookings(shipment);
            Finish(shipment);
        }
    }

    /// <summary>
    /// Assigns a booking chain to the shipment.
    ///
    /// The method first builds all feasible route-based booking options from
    /// the available service routes. It then solves a shortest-path problem
    /// from the shipment's origin port to its destination port, where each
    /// edge represents one feasible booking option on a service route and
    /// the edge weight is the sailing distance covered by that booking.
    ///
    /// The resulting path is converted into the shipment's associated bookings.
    /// </summary>
    private void AssignAssociatedBookings(Shipment shipment)
    {
        if (shipment == null)
            throw new ArgumentNullException(nameof(shipment));

        var demand = RequireNotNull(
            shipment.Demand,
            nameof(AssignAssociatedBookings),
            $"Shipment {shipment.Index} demand");

        var originPort = RequireNotNull(
            demand.OriginPort,
            nameof(AssignAssociatedBookings),
            $"Shipment {shipment.Index} demand origin port");

        var destinationPort = RequireNotNull(
            demand.DestinationPort,
            nameof(AssignAssociatedBookings),
            $"Shipment {shipment.Index} demand destination port");

        // Reset existing booking state in case this method is called more than once.
        shipment.AssociatedBookings.Clear();
        shipment.CurrentBookingIndex = null;

        Require(
            originPort != destinationPort,
            nameof(AssignAssociatedBookings),
            $"Shipment {shipment.Index} has same origin and destination port {originPort.Name}.");

        var candidateBookings = BuildAllCandidateBookings();

        var path = FindShortestBookingPath(
            originPort,
            destinationPort,
            candidateBookings);

        Require(
            path != null && path.Count > 0,
            nameof(AssignAssociatedBookings),
            $"No feasible booking chain found for shipment {shipment.Index} " +
            $"from {originPort.Name} to {destinationPort.Name}.");

        for (int i = 0; i < path!.Count; i++)
        {
            var edge = path[i];

            var booking = new Booking
            {
                SequenceIndex = i + 1,
                Shipment = shipment,
                ServiceRoute = edge.ServiceRoute,
                DepartureSegmentIndex = edge.DepartureSegmentIndex,
                ArrivalSegmentIndex = edge.ArrivalSegmentIndex
            };

            shipment.AssociatedBookings.Add(booking);
            edge.ServiceRoute.AssociatedBookings.Add(booking);
        }

        shipment.CurrentBookingIndex = shipment.AssociatedBookings
            .Min(booking => booking.SequenceIndex);
    }

    /// <summary>
    /// Builds all feasible single-booking options from all service routes.
    ///
    /// A single booking option:
    /// - uses one service route only;
    /// - starts from the departure port of one segment;
    /// - ends at the arrival port of a later segment on the same cyclic route;
    /// - may span multiple consecutive segments within that service route.
    ///
    /// For a service route with n segments, only paths of length 1 to n - 1
    /// segments are allowed. This avoids creating degenerate full-cycle bookings
    /// that start and end at the same port after completing an entire route cycle.
    /// </summary>
    private List<CandidateBookingEdge> BuildAllCandidateBookings()
    {
        var edges = new List<CandidateBookingEdge>();

        foreach (var serviceRoute in MaritimeDataContext.ServiceRoutes)
        {
            var segments = serviceRoute.Segments
                .OrderBy(segment => segment.SequenceIndex)
                .ToList();

            if (segments.Count == 0)
                continue;

            int n = segments.Count;

            for (int startIndex = 0; startIndex < n; startIndex++)
            {
                double cumulativeDistance = 0.0;
                var departurePort = segments[startIndex].AssociatedLeg.DeparturePort;

                for (int step = 1; step <= n - 1; step++)
                {
                    int segmentIndex = (startIndex + step - 1) % n;
                    var leg = segments[segmentIndex].AssociatedLeg;

                    cumulativeDistance += leg.SailingDistance;

                    var arrivalPort = leg.ArrivalPort;

                    if (departurePort == arrivalPort)
                        continue;

                    edges.Add(new CandidateBookingEdge
                    {
                        ServiceRoute = serviceRoute,
                        DeparturePort = departurePort,
                        ArrivalPort = arrivalPort,
                        DepartureSegmentIndex = startIndex % n + 1,
                        ArrivalSegmentIndex = (startIndex + step - 1) % n + 1,
                        TotalDistance = cumulativeDistance
                    });
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// Finds the shortest chain of booking options from origin to destination.
    ///
    /// This method applies Dijkstra's algorithm to a directed graph where:
    /// - nodes are ports;
    /// - edges are feasible route-based booking options; and
    /// - edge weights are total sailing distances.
    ///
    /// The returned path is a sequence of candidate booking edges that connects
    /// the origin port to the destination port.
    /// </summary>
    private List<CandidateBookingEdge>? FindShortestBookingPath(
        Port originPort,
        Port destinationPort,
        List<CandidateBookingEdge> allEdges)
    {
        var outgoing = allEdges
            .GroupBy(edge => edge.DeparturePort)
            .ToDictionary(group => group.Key, group => group.ToList());

        var ports = MaritimeDataContext.Ports.ToList();

        var distance = ports.ToDictionary(port => port, _ => double.PositiveInfinity);
        var previousEdge = new Dictionary<Port, CandidateBookingEdge?>();
        var unvisited = new HashSet<Port>(ports);

        distance[originPort] = 0.0;
        previousEdge[originPort] = null;

        while (unvisited.Count > 0)
        {
            var current = unvisited
                .OrderBy(port => distance[port])
                .First();

            if (double.IsPositiveInfinity(distance[current]))
                break;

            if (current == destinationPort)
                break;

            unvisited.Remove(current);

            if (!outgoing.TryGetValue(current, out var candidateEdges))
                continue;

            foreach (var edge in candidateEdges)
            {
                var next = edge.ArrivalPort;

                if (!unvisited.Contains(next))
                    continue;

                double alt = distance[current] + edge.TotalDistance;

                if (alt < distance[next])
                {
                    distance[next] = alt;
                    previousEdge[next] = edge;
                }
            }
        }

        if (!previousEdge.ContainsKey(destinationPort))
            return null;

        var path = new List<CandidateBookingEdge>();
        var cursor = destinationPort;

        while (cursor != originPort)
        {
            if (!previousEdge.TryGetValue(cursor, out var edge) || edge == null)
                return null;

            path.Add(edge);
            cursor = edge.DeparturePort;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Represents one feasible route-based booking option.
    ///
    /// A candidate booking edge connects a departure port to an arrival port
    /// using one service route. It may span one or more consecutive segments
    /// on that cyclic route.
    ///
    /// This object is used only during booking-chain construction and is
    /// converted into a real Booking once selected by the shortest-path search.
    /// </summary>
    private sealed class CandidateBookingEdge
    {
        /// <summary>
        /// Service route used by this candidate booking.
        /// </summary>
        public ServiceRoute ServiceRoute { get; set; } = null!;

        /// <summary>
        /// Port where the candidate booking starts.
        /// </summary>
        public Port DeparturePort { get; set; } = null!;

        /// <summary>
        /// Port where the candidate booking ends.
        /// </summary>
        public Port ArrivalPort { get; set; } = null!;

        /// <summary>
        /// Sequence index of the segment where the shipment is loaded.
        /// </summary>
        public int DepartureSegmentIndex { get; set; }

        /// <summary>
        /// Sequence index of the segment where the shipment is discharged.
        /// </summary>
        public int ArrivalSegmentIndex { get; set; }

        /// <summary>
        /// Total sailing distance covered by this candidate booking.
        /// </summary>
        public double TotalDistance { get; set; }
    }

    #region Statistics - TEU by Demand and Origin Port

    /// <summary>
    /// Time-weighted TEU counters grouped by demand OD pair.
    ///
    /// Each counter tracks the total TEU volume of shipments currently staying
    /// in this activity for the corresponding demand.
    ///
    /// The counter increases when a shipment starts this activity and decreases
    /// when the shipment departs from this activity.
    /// </summary>
    public Dictionary<Demand, HourCounter> HC_TeusByDemand { get; } = new();

    /// <summary>
    /// Time-weighted TEU counters grouped by demand origin port.
    ///
    /// Each counter tracks the total TEU volume of shipments currently staying
    /// in this activity for shipments whose demand starts from the corresponding
    /// origin port.
    ///
    /// The counter increases when a shipment starts this activity and decreases
    /// when the shipment departs from this activity.
    /// </summary>
    public Dictionary<Port, HourCounter> HC_TeusByOriginPort { get; } = new();

    protected override void Start(Shipment shipment)
    {
        base.Start(shipment);

        // Statistics: increase TEU volume for this demand when the shipment enters this activity.
        var demand = RequireNotNull(
            shipment.Demand,
            nameof(Start),
            $"Demand of Shipment {shipment.Index}");

        if (!HC_TeusByDemand.TryGetValue(demand, out var demandCounter))
        {
            demandCounter = AddHourCounter();
            HC_TeusByDemand[demand] = demandCounter;
        }

        demandCounter.ObserveChange(shipment.TeuSize);

        // Statistics: increase TEU volume for this demand origin port.
        var originPort = RequireNotNull(
            demand.OriginPort,
            nameof(Start),
            $"Origin port of Demand {demand}");

        if (!HC_TeusByOriginPort.TryGetValue(originPort, out var originPortCounter))
        {
            originPortCounter = AddHourCounter();
            HC_TeusByOriginPort[originPort] = originPortCounter;
        }

        originPortCounter.ObserveChange(shipment.TeuSize);
    }

    public override void Depart(Shipment shipment)
    {
        if (F_LoadsFinished.Contains(shipment))
        {
            base.Depart(shipment);

            // Statistics: decrease TEU volume for this demand when the shipment leaves this activity.
            var demand = RequireNotNull(
                shipment.Demand,
                nameof(Depart),
                $"Demand of Shipment {shipment.Index}");

            if (!HC_TeusByDemand.TryGetValue(demand, out var demandCounter))
            {
                ThrowActivityException(
                    nameof(Depart),
                    $"Cannot update TEU statistics for Shipment {shipment.Index} because no HourCounter exists for Demand {demand}. " +
                    "This indicates that the shipment may not have started this activity before departure.");
            }

            demandCounter.ObserveChange(-shipment.TeuSize);

            // Statistics: decrease TEU volume for this demand origin port.
            var originPort = RequireNotNull(
                demand.OriginPort,
                nameof(Depart),
                $"Origin port of Demand {demand}");

            if (!HC_TeusByOriginPort.TryGetValue(originPort, out var originPortCounter))
            {
                ThrowActivityException(
                    nameof(Depart),
                    $"Cannot update TEU statistics for Shipment {shipment.Index} because no HourCounter exists for origin port {originPort}. " +
                    "This indicates that the shipment may not have started this activity before departure.");
            }

            originPortCounter.ObserveChange(-shipment.TeuSize);
        }
    }

    #endregion
}