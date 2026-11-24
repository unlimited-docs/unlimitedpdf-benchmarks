# Project instructions

Maintain a neutral, independently verifiable benchmark suite. UnlimitedPDF is
the baseline, not a predetermined winner. Use published packages and public
APIs; do not optimize one implementation by disadvantaging another.

Core holds generation and validation; Runner holds measured operations; Tests
checks correctness. Keep preparation and validation outside timed methods.
Compare equivalent inputs, output semantics and serialization, dispose
resources, and validate output independently. Document differences and avoid
ranking failed, unsupported or unexecuted workloads.

Use .NET 10, nullable C#, central package versions and lock files. Review each
new or upgraded package and transitive dependency for proprietary commercial
use, license notices and vulnerabilities before inclusion. Preserve the
repository's Apache 2.0 license. Do not commit credentials or generated
artifacts. Run locked restore, Release build and tests as described in README.

Record raw results and environment; never fabricate measurements or conflate
managed allocations with process or native memory. Add focused tests and
document limitations and reproducible commands when extending scenarios.
