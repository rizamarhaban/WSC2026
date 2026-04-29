namespace SimulationChallenge2026;

/// <summary>
/// Berth berthing activity.
/// 
/// This activity represents the berthing and unberthing time
/// associated with a berth. Start and finish are both unconditional.
/// </summary>
public class Berth_Berthing : ActivityHandler<Berth>
{
    public Berth_Berthing(int seed = 0)
        : base(id: nameof(Berth_Berthing), seed: seed)
    {
        // Fixed berthing-related duration
        T_Duration = (berth, rs) => TimeSpan.FromHours(3.0);
    }
}