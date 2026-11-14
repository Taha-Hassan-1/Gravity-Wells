using System.Runtime.CompilerServices;

// The stored solution of a level is internal so Game/ can never read it; the tests and the
// Editor tuning tools are the only other code allowed to.
[assembly: InternalsVisibleTo("GravityWells.Tests.EditMode")]
[assembly: InternalsVisibleTo("GravityWells.Editor")]
