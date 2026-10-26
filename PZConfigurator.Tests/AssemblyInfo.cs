using Xunit;

// ConfigParser exposes static test seams (UtcNow, BeforeRename) that every VM/parser
// test uses; xUnit would run the classes in parallel and race on them.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
