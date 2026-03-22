# Implementation Plan: SPARQL Update Operations

## Overview

Add delete and update capabilities to the append-only QuadStore using a tombstone-based soft-delete mechanism, then wire SPARQL 1.1 Update support through the dotNetRDF adapter. Implementation proceeds bottom-up: bitmap index removal → tombstone infrastructure → core Delete method → adapter changes → SPARQL Update parsing → persistence → thread safety tests.

## Tasks

- [ ] 1. Add BitmapIndex.Remove method
  - [ ] 1.1 Implement `Remove(int dictId, long row)` on `BitmapIndex`
    - Add a public `Remove` method to `src/QuadStore.Core/BitmapIndex.cs`
    - If `dictId` is not in `_bitmaps`, no-op. If row is not in the bitmap, no-op.
    - Use `Roaring32Bitmap.Remove((uint)row)` to remove the row ID
    - _Requirements: 2.1, 2.3_

  - [ ]* 1.2 Write unit tests for BitmapIndex.Remove
    - Test removing an existing row ID
    - Test removing a non-existent row ID (no-op)
    - Test removing from a non-existent dictionary ID (no-op)
    - _Requirements: 2.3_

- [ ] 2. Implement tombstone infrastructure and QuadStore.Delete
  - [ ] 2.1 Add tombstone HashSet and modify Query to skip tombstoned rows
    - Add `private readonly HashSet<long> _tombstones = new();` to `QuadStore`
    - In the `Query` method, after materializing each row, check `_tombstones.Contains(row)` and skip if true
    - The check should happen before calling `_encoder.GetString` for efficiency
    - _Requirements: 1.2, 1.3_

  - [ ] 2.2 Implement `QuadStore.Delete` method
    - Add `public int Delete(string? subject = null, string? predicate = null, string? obj = null, string? graph = null)`
    - Acquire write lock via `_lock.EnterWriteLock()`
    - Find matching rows by reading columns directly (iterate `_rowCount`, check each non-null filter against encoded column values, skip already-tombstoned rows)
    - For each matching row: add to `_tombstones`, call `BitmapIndex.Remove` on all four indexes (S, P, O, G)
    - Return count of newly deleted rows
    - When all four filters are null, delete all non-tombstoned rows
    - When pattern matches zero rows, return 0 without error
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2_

  - [ ]* 2.3 Write property test: delete-then-query exclusion
    - **Property 1: Delete-then-query exclusion**
    - Generate random quads, append them, generate a random delete pattern from existing values, delete, verify Query returns no deleted quads and all non-deleted quads remain
    - **Validates: Requirements 1.1, 1.2, 2.2**

  - [ ]* 2.4 Write unit tests for QuadStore.Delete
    - Test delete single quad by all four components
    - Test delete by each individual filter dimension (subject-only, predicate-only, etc.)
    - Test delete-all with all null filters
    - Test delete non-existent pattern returns 0
    - Test delete with multiple matching rows
    - Test that Query excludes deleted quads
    - _Requirements: 1.1, 1.2, 1.4, 1.6_

- [ ] 3. Checkpoint — Core delete works
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Implement tombstone persistence (SaveAll/LoadAll)
  - [ ] 4.1 Persist tombstones in SaveAll and restore in LoadAll
    - In `SaveAll`, write `_tombstones` to `tombstones.bin` in the store root: `[int32: version=1][int32: count][int64: rowId × count]` with row IDs sorted
    - In `LoadAll`, read `tombstones.bin` back into `_tombstones` HashSet; if file doesn't exist, clear the set
    - _Requirements: 9.1, 9.2_

  - [ ]* 4.2 Write property test: SaveAll/LoadAll round-trip preserves deletion state
    - **Property 6: SaveAll/LoadAll round-trip preserves deletion state**
    - Generate random appends and deletes, SaveAll, create new QuadStore from same directory, compare Query results
    - **Validates: Requirements 9.1, 9.2, 9.3**

  - [ ]* 4.3 Write unit tests for tombstone persistence
    - Test SaveAll writes tombstones.bin, LoadAll restores them
    - Test round-trip: append, delete, save, reload, query returns correct results
    - Test loading a store with no tombstones.bin file (clean start)
    - _Requirements: 9.1, 9.2, 9.3_

- [ ] 5. Update StorageProvider: DeleteGraph and UpdateGraph with removals
  - [ ] 5.1 Change `DeleteSupported` to return `true` and update `IOBehaviour`
    - Change `DeleteSupported` property to return `true`
    - Add `IOBehaviour.CanUpdateDeleteTriples` to the `IOBehaviour` property
    - _Requirements: 3.5_

  - [ ] 5.2 Implement `DeleteGraph(Uri)` and `DeleteGraph(string)` to delegate to `QuadStore.Delete`
    - Remove the `throw RdfStorageException` from both overloads
    - Normalise the graph URI (strip angle brackets), then call `_store.Delete(graph: normalised)`
    - Also try deleting with the angle-bracketed form to handle data stored in either format
    - If graph URI is null or empty, delete quads in the default graph
    - _Requirements: 3.1, 3.3, 3.4_

  - [ ] 5.3 Implement `UpdateGraph` with removals support
    - In `UpdateGraphInternal`, instead of throwing on non-empty removals, iterate each removal triple
    - Convert each triple to a fully-specified delete pattern: `_store.Delete(subject, predicate, obj, graph)` using `NodeToString` for each component
    - Process removals before additions
    - If a removal triple doesn't exist, the delete is a no-op (returns 0)
    - Keep backward compatibility: null removals or empty removals still work as before
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ]* 5.4 Write property test: deleted graph excluded from ListGraphs
    - **Property 2: Deleted graph excluded from ListGraphs**
    - Generate quads across multiple graphs, delete one graph, verify ListGraphNames excludes it and others remain
    - **Validates: Requirements 3.1, 3.2**

  - [ ]* 5.5 Write property test: UpdateGraph removals delete matching quads
    - **Property 3: UpdateGraph removals delete matching quads**
    - Generate triples, add via SaveGraph, remove subset via UpdateGraph, verify LoadGraph reflects removals
    - **Validates: Requirements 4.1**

  - [ ]* 5.6 Write unit tests for DeleteGraph and UpdateGraph with removals
    - Test DeleteGraph(Uri) and DeleteGraph(string) both work
    - Test DeleteSupported returns true
    - Test IOBehaviour includes CanUpdateDeleteTriples
    - Test ListGraphs excludes deleted graph
    - Test UpdateGraph: removals processed before additions
    - Test UpdateGraph: removal of non-existent triple is no-op
    - Test UpdateGraph: null removals still works (backward compat)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4_

- [ ] 6. Checkpoint — Adapter delete operations work
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Implement SPARQL Update parsing and execution
  - [ ] 7.1 Implement `Update(string sparqlUpdate)` with INSERT DATA and DELETE DATA support
    - Remove the `throw RdfStorageException` from `Update`
    - Use `VDS.RDF.Parsing.SparqlUpdateParser` to parse the SPARQL Update string into a `SparqlUpdateCommandSet`
    - Walk each command in the set:
      - `InsertDataCommand`: iterate `DataPattern.Triples` (and graph-scoped triples via `GraphPattern`), append each quad via `_store.Append`
      - `DeleteDataCommand`: iterate `DataPattern.Triples` (and graph-scoped triples), delete each quad via `_store.Delete`
    - For INSERT DATA without GRAPH clause, use empty string as graph URI
    - For INSERT DATA with GRAPH clause, use the specified graph URI
    - Wrap parsing in try/catch; on parse failure throw `RdfStorageException` with descriptive message
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_

  - [ ] 7.2 Add DELETE/INSERT WHERE support
    - For `ModifyCommand`: evaluate the WHERE pattern by building a snapshot dataset (reuse `CreateQueryProcessor` approach), run the WHERE clause, collect bindings
    - Apply DELETE template: for each binding, instantiate the DELETE template triples and call `_store.Delete` for each
    - Apply INSERT template: for each binding, instantiate the INSERT template triples and call `_store.Append` for each
    - Collect all bindings before applying any changes (snapshot semantics)
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [ ] 7.3 Add DROP and CLEAR support
    - For `DropCommand`: dispatch based on graph specifier
      - `DROP GRAPH <uri>`: call `_store.Delete(graph: uri)`. Without SILENT, check if graph exists first via `ListGraphNames`; if not, throw `RdfStorageException`. With SILENT, no-op if graph doesn't exist.
      - `DROP ALL`: call `_store.Delete()` (all nulls)
      - `DROP DEFAULT`: call `_store.Delete(graph: "")` (empty string graph)
    - For `ClearCommand`: same behavior as DROP (CLEAR GRAPH, CLEAR ALL, CLEAR DEFAULT)
    - For unsupported commands (LOAD, CREATE, ADD, MOVE, COPY): throw `RdfStorageException` with descriptive message
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [ ]* 7.4 Write property test: INSERT DATA round-trip
    - **Property 4: INSERT DATA round-trip**
    - Generate valid RDF triples and graph URI, build SPARQL INSERT DATA string, execute via Update, verify quads are queryable
    - **Validates: Requirements 5.1, 5.2, 5.3**

  - [ ]* 7.5 Write property test: DELETE DATA removes specified quads
    - **Property 5: DELETE DATA removes specified quads**
    - Generate quads, append them, build SPARQL DELETE DATA string, execute, verify removal while other quads remain
    - **Validates: Requirements 6.1, 6.2, 6.3**

  - [ ]* 7.6 Write unit tests for SPARQL Update operations
    - Test INSERT DATA with GRAPH clause
    - Test INSERT DATA without GRAPH clause (default graph)
    - Test INSERT DATA with invalid syntax throws RdfStorageException
    - Test DELETE DATA with GRAPH clause
    - Test DELETE DATA for non-existent quad is no-op
    - Test DELETE DATA with invalid syntax throws RdfStorageException
    - Test DELETE/INSERT WHERE with pattern evaluation
    - Test DELETE/INSERT WHERE snapshot semantics (bindings collected before changes)
    - Test DROP GRAPH with existing graph
    - Test DROP GRAPH without SILENT on missing graph throws RdfStorageException
    - Test DROP GRAPH with SILENT on missing graph succeeds
    - Test DROP ALL deletes everything
    - Test DROP DEFAULT deletes default graph
    - Test CLEAR GRAPH, CLEAR ALL, CLEAR DEFAULT
    - Test unsupported command (e.g., LOAD) throws RdfStorageException
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

- [ ] 8. Checkpoint — SPARQL Update operations work
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Thread safety for delete and update operations
  - [ ] 9.1 Verify and harden thread safety in Delete method
    - Ensure `Delete` acquires exclusive write lock via `_lock.EnterWriteLock()` and releases in `finally`
    - Ensure `Query` read lock correctly excludes tombstoned rows under concurrent access
    - _Requirements: 10.1, 10.2_

  - [ ]* 9.2 Write concurrency stress tests
    - Test concurrent deletes and queries maintain data consistency
    - Test concurrent appends and deletes with no lost operations
    - Test that no phantom reads occur within a single lock acquisition
    - _Requirements: 10.1, 10.2, 10.3_

- [ ] 10. Update XML doc comments and remove stale documentation
  - Remove or update the "append-only" references in `QuadStoreStorageProvider` XML doc comments
  - Update the class-level remarks to reflect delete/update support
  - Update `UpdateGraphInternal` doc to reflect removals support
  - Update `Update` method doc to reflect SPARQL Update support
  - _Requirements: 3.4, 3.5, 4.1, 5.1_

- [ ] 11. Final checkpoint — All tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# throughout, matching the existing codebase
- FsCheck.Xunit 3.3.2 is already available in the test project for property-based tests
