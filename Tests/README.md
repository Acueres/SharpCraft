# Rendering allocation tests

Run the suite with:

```sh
dotnet test Tests/Tests.csproj --no-restore
```

The suite runs without an SDL window or GPU initialization. `FakeBufferDevice`
implements the buffer creation/release boundary with opaque handles and records
requested sizes, usages, and releases.

Coverage includes:

- CPU range alignment, rounding, exhaustion, buddy coalescing, and handle identity;
- per-page slot exhaustion, the full 16-bit ID range, slot reuse, failed range
  reservations, and rejection of fabricated GPU wrappers;
- arena growth from either range or slot exhaustion, oversized stream allocation
  order, live handles during reclamation, one warm reserve, and buffer disposal;
- record-count-to-byte conversion, overflow checks, and capacity accounting;
- managed face, slot, and camera layouts plus slot-ID stamping.
