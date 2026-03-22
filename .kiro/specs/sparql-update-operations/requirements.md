# Requirements Document

## Introduction

QuadStore is currently an append-only RDF quad store. The `QuadStoreStorageProvider` adapter throws `RdfStorageException` for `DeleteGraph`, `UpdateGraph` (with removals), and `Update` (SPARQL Update) operations. This feature adds delete and update capabilities to the core `QuadStore` and wires them through the dotNetRDF adapter, moving towards compliance with the SPARQL 1.1 Update standard.

## Glossary

- **QuadStore**: The core in-memory RDF quad store class (`TripleStore.Core.QuadStore`) that manages columnar storage, dictionary encoding, and bitmap indexes.
- **StorageProvider**: The dotNetRDF adapter class (`QuadStoreStorageProvider`) that implements `IStorageProvider`, `IQueryableStorage`, and `IUpdateableStorage`.
- **Quad**: A tuple of four string values (subject, predicate, object, graph) representing an RDF statement in a named graph.
- **BitmapIndex**: The roaring bitmap index (`BitmapIndex`) mapping dictionary-encoded IDs to sets of row IDs.
- **DictionaryEncoder**: The bidirectional string-to-integer encoder (`DictionaryEncoder`) used for compact columnar storage.
- **EncodedColumn**: A memory-mapped integer column (`EncodedColumn`) storing dictionary-encoded IDs for one quad component (S, P, O, or G).
- **SparqlEngine**: The minimal SPARQL engine (`MinimalSparqlEngine`) that translates SPARQL patterns into QuadStore queries.
- **Tombstone**: A per-row deletion marker indicating that a quad has been logically deleted without physically removing it from columnar storage.
- **SPARQL_Update**: A SPARQL 1.1 Update request string containing one or more update operations (INSERT DATA, DELETE DATA, DELETE/INSERT WHERE, DROP, CLEAR, etc.).

## Requirements

### Requirement 1: Delete Quads by Pattern

**User Story:** As a developer, I want to delete quads from the QuadStore by specifying a pattern of subject, predicate, object, and/or graph filters, so that I can remove specific data without rebuilding the entire store.

#### Acceptance Criteria

1. WHEN a delete pattern with one or more non-null filters is provided, THE QuadStore SHALL mark all matching quads as logically deleted.
2. WHEN a quad has been marked as deleted, THE QuadStore SHALL exclude that quad from all subsequent Query results.
3. THE QuadStore SHALL use a Tombstone mechanism to track deleted rows without physically removing data from the EncodedColumn files.
4. WHEN a delete pattern matches zero quads, THE QuadStore SHALL complete the operation without error and without modifying any data.
5. THE QuadStore SHALL acquire an exclusive write lock before performing any delete operation and release the lock after the operation completes.
6. WHEN a null filter is provided for all four components (subject, predicate, object, graph), THE QuadStore SHALL delete all quads in the store.

### Requirement 2: Update Bitmap Indexes on Delete

**User Story:** As a developer, I want bitmap indexes to remain consistent after deletions, so that queries continue to return correct results.

#### Acceptance Criteria

1. WHEN a quad is marked as deleted, THE BitmapIndex SHALL remove the corresponding row ID from the bitmap entries for all four components (subject, predicate, object, graph).
2. AFTER a delete operation completes, THE QuadStore SHALL return identical results from Query whether the query uses bitmap intersection or full scan.
3. THE BitmapIndex SHALL support a Remove operation that removes a single row ID from the bitmap for a given dictionary ID.

### Requirement 3: Delete Graph

**User Story:** As a developer, I want to delete all quads belonging to a specific named graph, so that I can remove an entire graph from the store.

#### Acceptance Criteria

1. WHEN a graph URI is provided, THE QuadStore SHALL mark all quads with a matching graph component as logically deleted.
2. AFTER a graph is deleted, THE StorageProvider SHALL exclude the deleted graph URI from the results of ListGraphs and ListGraphNames.
3. WHEN the specified graph URI does not exist in the store, THE QuadStore SHALL complete the operation without error.
4. THE StorageProvider SHALL expose DeleteGraph operations through both the `DeleteGraph(Uri)` and `DeleteGraph(string)` overloads without throwing RdfStorageException.
5. THE StorageProvider SHALL report `DeleteSupported` as true.

### Requirement 4: UpdateGraph with Removals

**User Story:** As a developer, I want to use the dotNetRDF UpdateGraph method with both additions and removals, so that I can modify graph contents through the standard dotNetRDF API.

#### Acceptance Criteria

1. WHEN a removals collection is provided to UpdateGraph, THE StorageProvider SHALL delete the matching quads from the QuadStore instead of throwing RdfStorageException.
2. WHEN both additions and removals are provided, THE StorageProvider SHALL process removals before additions within the same operation.
3. WHEN a removal triple does not exist in the specified graph, THE StorageProvider SHALL skip that triple without error.
4. WHEN only additions are provided with null removals, THE StorageProvider SHALL append the triples as it does currently.

### Requirement 5: SPARQL UPDATE — INSERT DATA

**User Story:** As a developer, I want to execute SPARQL INSERT DATA commands through the StorageProvider, so that I can add triples using standard SPARQL Update syntax.

#### Acceptance Criteria

1. WHEN a SPARQL INSERT DATA command is provided, THE StorageProvider SHALL parse the command and append the specified quads to the QuadStore.
2. WHEN the INSERT DATA command specifies a GRAPH clause, THE StorageProvider SHALL use the specified graph URI as the graph component for each inserted quad.
3. WHEN the INSERT DATA command contains no GRAPH clause, THE StorageProvider SHALL insert quads into the default graph (empty string graph URI).
4. IF the INSERT DATA command contains invalid syntax, THEN THE StorageProvider SHALL throw an RdfStorageException with a descriptive error message.

### Requirement 6: SPARQL UPDATE — DELETE DATA

**User Story:** As a developer, I want to execute SPARQL DELETE DATA commands through the StorageProvider, so that I can remove specific triples using standard SPARQL Update syntax.

#### Acceptance Criteria

1. WHEN a SPARQL DELETE DATA command is provided, THE StorageProvider SHALL parse the command and delete the specified quads from the QuadStore.
2. WHEN the DELETE DATA command specifies a GRAPH clause, THE StorageProvider SHALL scope the deletion to the specified graph.
3. WHEN a specified quad does not exist in the store, THE StorageProvider SHALL skip it without error.
4. IF the DELETE DATA command contains invalid syntax, THEN THE StorageProvider SHALL throw an RdfStorageException with a descriptive error message.

### Requirement 7: SPARQL UPDATE — DELETE/INSERT WHERE

**User Story:** As a developer, I want to execute SPARQL DELETE/INSERT WHERE commands, so that I can perform pattern-based modifications using standard SPARQL Update syntax.

#### Acceptance Criteria

1. WHEN a DELETE WHERE command is provided, THE StorageProvider SHALL evaluate the WHERE pattern against the store and delete all matching quads.
2. WHEN a DELETE/INSERT WHERE command is provided, THE StorageProvider SHALL evaluate the WHERE pattern, delete the matching quads from the DELETE template, and insert the quads from the INSERT template.
3. THE StorageProvider SHALL evaluate the WHERE clause and collect all matching bindings before applying any deletions or insertions.
4. IF the DELETE/INSERT WHERE command contains invalid syntax, THEN THE StorageProvider SHALL throw an RdfStorageException with a descriptive error message.

### Requirement 8: SPARQL UPDATE — DROP and CLEAR

**User Story:** As a developer, I want to execute SPARQL DROP and CLEAR commands, so that I can remove entire graphs or all data using standard SPARQL Update syntax.

#### Acceptance Criteria

1. WHEN a DROP GRAPH command is provided with a graph URI, THE StorageProvider SHALL delete all quads in the specified graph.
2. WHEN a CLEAR GRAPH command is provided with a graph URI, THE StorageProvider SHALL delete all quads in the specified graph.
3. WHEN a DROP ALL or CLEAR ALL command is provided, THE StorageProvider SHALL delete all quads in the store.
4. WHEN a DROP DEFAULT or CLEAR DEFAULT command is provided, THE StorageProvider SHALL delete all quads in the default graph.
5. WHEN DROP GRAPH is used with the SILENT keyword and the graph does not exist, THE StorageProvider SHALL complete without error.
6. IF DROP GRAPH is used without the SILENT keyword and the graph does not exist, THEN THE StorageProvider SHALL throw an RdfStorageException.

### Requirement 9: Persistence of Deletions

**User Story:** As a developer, I want deletions to survive a SaveAll/LoadAll cycle, so that the store state is consistent after persistence and reload.

#### Acceptance Criteria

1. WHEN SaveAll is called after quads have been deleted, THE QuadStore SHALL persist the Tombstone state to disk.
2. WHEN LoadAll is called on a store with persisted Tombstones, THE QuadStore SHALL restore the Tombstone state and exclude deleted quads from queries.
3. FOR ALL QuadStore states, saving then loading SHALL produce a store that returns equivalent Query results to the original (round-trip property).

### Requirement 10: Thread Safety for Delete and Update Operations

**User Story:** As a developer, I want delete and update operations to be thread-safe, so that concurrent access does not corrupt the store.

#### Acceptance Criteria

1. THE QuadStore SHALL acquire an exclusive write lock via ReaderWriterLockSlim before performing any delete operation.
2. WHILE a delete operation holds the write lock, THE QuadStore SHALL block all concurrent read and write operations.
3. WHEN multiple threads perform concurrent deletes and queries, THE QuadStore SHALL maintain data consistency with no lost deletes or phantom reads within a single lock acquisition.
