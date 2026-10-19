namespace Cellblock.Core
{
    // Read-only views of the live staff fight for presentation (Claude, 2026-10-19): who the controlled officer is
    // squared up with, if anyone. Rules stay in StaffCombat.cs (Codex).
    public sealed partial class World
    {
        public string StaffFightOpponentId { get { return staffFight == null ? "" : staffFight.ResidentId ?? ""; } }
    }
}
