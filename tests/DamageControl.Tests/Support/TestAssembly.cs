using Xunit;

// Core has process-wide static state (DcLog's sink and once-keys). Running test classes one at a time
// keeps tests that touch it deterministic; the whole suite takes well under a second.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
