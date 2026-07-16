using Xunit;

// WebApplicationFactory<Web.Program> boots the real Program.cs entry point, which uses
// Serilog's shared static Log.Logger and closes it in a finally block. Running multiple
// factories concurrently (the default xUnit behavior across test collections) risks one
// factory's shutdown tearing down the logger while another is still starting up.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
