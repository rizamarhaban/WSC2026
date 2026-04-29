using O2DESNet;

namespace SimulationChallenge2026;

internal class Model : Sandbox
{
    public MaritimeDataContext DataContext { get; }

    // --- Generators ---
    public Shipment_Generator Shipment_Generator { get; }
    public Vessel_Generator Vessel_Generator { get; }
    public Berth_Generator Berth_Generator { get; }

    // --- Activities ---
    public Shipment_WaitingForLoadingAtOriginPort Shipment_WaitingForLoadingAtOriginPort { get; }
    public Shipment_WaitingForLoadingAtTransshipmentPort Shipment_WaitingForLoadingAtTransshipmentPort { get; }
    public Shipment_BeingTransported Shipment_BeingTransported { get; }
    public Vessel_AwaitingInstructions Vessel_AwaitingInstructions { get; }
    public Vessel_Sailing Vessel_Sailing { get; }
    public Vessel_QueuingForBerth Vessel_QueuingForBerth { get; }
    public Vessel_BeingServed Vessel_BeingServed { get; }
    public Berth_Idle Berth_Idle { get; }
    public Berth_Berthing Berth_Berthing { get; }
    public Berth_HandlingCargo Berth_HandlingCargo { get; }

    public Model(MaritimeDataContext dataContext, int seed = 0)
        : base(seed: seed)
    {
        DataContext = dataContext
            ?? throw new ArgumentNullException(nameof(dataContext));

        // --- Generators ---
        Shipment_Generator = AddChild(
            new Shipment_Generator(DataContext, seed: DefaultRS.Next()) { EnableLog = false });

        Vessel_Generator = AddChild(
            new Vessel_Generator(DataContext, seed: DefaultRS.Next()) { EnableLog = false });

        Berth_Generator = AddChild(
            new Berth_Generator(DataContext, seed: DefaultRS.Next()) { EnableLog = false });

        // --- Activities ---
        Shipment_WaitingForLoadingAtOriginPort = AddChild(
            new Shipment_WaitingForLoadingAtOriginPort(DataContext, seed: DefaultRS.Next()) { EnableLog = false });

        Shipment_WaitingForLoadingAtTransshipmentPort = AddChild(
            new Shipment_WaitingForLoadingAtTransshipmentPort(seed: DefaultRS.Next()) { EnableLog = false });

        Shipment_BeingTransported = AddChild(
            new Shipment_BeingTransported(seed: DefaultRS.Next()) { EnableLog = false });

        Vessel_AwaitingInstructions = AddChild(
            new Vessel_AwaitingInstructions(seed: DefaultRS.Next()) { EnableLog = false });

        Vessel_Sailing = AddChild(
            new Vessel_Sailing(seed: DefaultRS.Next()) { EnableLog = false });

        Vessel_QueuingForBerth = AddChild(
            new Vessel_QueuingForBerth(seed: DefaultRS.Next()) { EnableLog = false });

        Vessel_BeingServed = AddChild(
            new Vessel_BeingServed(seed: DefaultRS.Next()) { EnableLog = false });

        Berth_Idle = AddChild(
            new Berth_Idle(seed: DefaultRS.Next()) { EnableLog = false });

        Berth_Berthing = AddChild(
            new Berth_Berthing(seed: DefaultRS.Next()) { EnableLog = false });

        Berth_HandlingCargo = AddChild(
            new Berth_HandlingCargo(seed: DefaultRS.Next()) { EnableLog = false });

        // =========================================================
        // 🔗 Event Wiring (Core System Logic)
        // =========================================================

        // --- Shipment flow ---
        Shipment_Generator.ConnectTo(Shipment_WaitingForLoadingAtOriginPort);
        Shipment_WaitingForLoadingAtOriginPort.ConnectTo(Shipment_BeingTransported);
        Shipment_BeingTransported.ConnectTo(Shipment_WaitingForLoadingAtTransshipmentPort, shipment => !shipment.IsAtLastBooking());
        Shipment_BeingTransported.Terminate(shipment => shipment.IsAtLastBooking());
        Shipment_WaitingForLoadingAtTransshipmentPort.ConnectTo(Shipment_BeingTransported);

        // --- Vessel flow ---
        Vessel_Generator.ConnectTo(Vessel_AwaitingInstructions);
        Vessel_AwaitingInstructions.ConnectTo(Vessel_Sailing);
        Vessel_Sailing.ConnectTo(Vessel_QueuingForBerth);
        Vessel_QueuingForBerth.ConnectTo(Vessel_BeingServed);
        Vessel_BeingServed.ConnectTo(Vessel_Sailing, vessel => true);
        //Vessel_BeingServed.ConnectTo(Vessel_AwaitingInstructions, vessel => true);

        // --- Berth flow ---
        Berth_Generator.ConnectTo(Berth_Idle);
        Berth_Idle.ConnectTo(Berth_Berthing);
        Berth_Berthing.ConnectTo(Berth_HandlingCargo);
        Berth_HandlingCargo.ConnectTo(Berth_Idle);

        // =========================================================
        // 🔔 Signal Wiring (Cross-Flow Coordination)
        // =========================================================

        // Shipment readiness at port enables vessel service
        Shipment_WaitingForLoadingAtOriginPort.OnFinish.Add(Vessel_BeingServed.SignalStart);
        Shipment_WaitingForLoadingAtTransshipmentPort.OnFinish.Add(Vessel_BeingServed.SignalStart);

        // Vessel service drives shipment transport state transition
        Vessel_BeingServed.OnStart.Add(Shipment_BeingTransported.SignalStart);
        Vessel_BeingServed.OnFinish.Add(Shipment_BeingTransported.SignalFinish);

        // Vessel queueing and berth assignment coordination
        Vessel_QueuingForBerth.OnStart.Add(Berth_Idle.SignalFinish);
        Berth_Berthing.OnStart.Add(Vessel_QueuingForBerth.SignalFinish);

        // Vessel service and berth cargo handling coordination
        Vessel_BeingServed.OnStart.Add(Berth_HandlingCargo.SignalStart);
        Berth_HandlingCargo.OnFinish.Add(Vessel_BeingServed.SignalFinish);

    }
}