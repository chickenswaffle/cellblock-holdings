static partial class Program
{
    // Claude's resident-lives scope owns this hook; population pressure checks run from it until Lives checks exist.
    static partial void RunLivesChecks()
    {
        RunPressureChecks();
        RunEconomyChecks();
        RunIntelChecks();
        RunKitchenChecks();
        RunCounselChecks();
        RunRoleChecks();
        RunPlacementChecks();
    }
}
