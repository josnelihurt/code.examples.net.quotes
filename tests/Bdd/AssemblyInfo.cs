// The specs share one stack and one catalog database, and the health-readiness journey
// deliberately pauses that database: scenarios must run sequentially or the pause would
// starve concurrently running journeys of their database calls.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
