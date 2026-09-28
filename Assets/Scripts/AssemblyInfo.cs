using System.Runtime.CompilerServices;

// Lets test assemblies reach `internal` test seams (e.g. EraProgressionManager
// save flushing and catalog override) without making them part of the public API.
[assembly: InternalsVisibleTo("HaoKhiSuViet.Tests.Support")]
[assembly: InternalsVisibleTo("HaoKhiSuViet.Tests.EditMode")]
[assembly: InternalsVisibleTo("HaoKhiSuViet.Tests.PlayMode")]
