using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Analytics;

public record ChainDebugInfo(Guid ChainId, IReadOnlyList<ChainJobEntry> Jobs);

public record ChainJobEntry(string JobKey, string Status);
