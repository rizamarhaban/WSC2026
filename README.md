# WSC Simulation Challenge 2026

This repository contains the baseline C# simulation model for the **WSC Simulation Challenge 2026**.

The project provides an initial integrated simulation framework, including maritime data context, simulation model components, activity handlers, generators, and process logic. The model integration has been completed at the structural level, while further debugging, testing, calibration, and validation are still required.

## Repository Purpose

This repository is intended to support the development and release of the WSC Simulation Challenge 2026 baseline model. It provides a common starting point for organising the simulation logic, domain entities, and decision interfaces that may be used by participants.

## Project Structure

The solution currently includes the following major components:

```text
SimulationChallenge2026/
├── MaritimeDataContext/
│   ├── Berth.cs
│   ├── Booking.cs
│   ├── Demand.cs
│   ├── Leg.cs
│   ├── MaritimeDataContext.cs
│   ├── PartialServiceRoute.cs
│   ├── Port.cs
│   ├── ServiceRoute.cs
│   ├── Shipment.cs
│   ├── Vessel.cs
│   └── VesselClass.cs
│
├── SimulationModel/
│   ├── ActivityHandler.cs
│   ├── Generator.cs
│   ├── Model.cs
│   ├── Process.cs
│   ├── Queueing.cs
│   └── Activity and generator classes
│
└── SimulationChallenge2026.csproj
```
