using Xunit;

// Avalonia headless shares one dispatcher; parallel tests cause PushFrame failures on CI Linux.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
