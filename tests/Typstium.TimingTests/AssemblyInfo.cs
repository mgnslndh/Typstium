// Every test here asserts wall-clock bounds on CPU-bound compiles, so the tests run one at a time; build.ps1 also
// runs this project alone, after the parallel unit and integration tests.
[assembly: NotInParallel]
