# SinglePassTrigLoader Implementation Plan

**TL;DR:** Create new `SinglePassTrigLoader` class using ANTLR parser with direct single-pass quad loading. Keep existing `TriGLoader` and all dotNetRDF dependencies intact until new loader is fully validated. Deletion phase deferred to later.

## Steps

1. **Create new file** `src/TripleStore.Core/SinglePassTrigLoader.cs` as standalone class—public interface mirrors `TriGLoader`: constructor taking `QuadStore`, methods `LoadFromFile()`, `LoadFromStream()`, `LoadFromTextReader()`, `LoadFromString()`, `GetLoadedQuadCount()`.

2. **Import ANTLR and standard libraries only** - `using System`, `System.IO`, `System.Collections.Generic`, `Antlr4.Runtime`; no `VDS.RDF` references.

3. **Implement lexer/parser initialization in `LoadFromTextReader()`** - Create `AntlrInputStream` from reader, instantiate `TrigLexer`, wrap in `CommonTokenStream`, create `TrigParser`, call `parser.trigDoc()` to get parse tree, instantiate `TriGVisitorLoader` visitor and call `Visit()` on tree.

4. **Build `TriGVisitorLoader` nested class** extending `TrigParserBaseVisitor<object?>` - Maintain parser state (`_prefixes` dict, `_baseUri`, `_currentGraph`, `_blankNodeCounter`, `_blankNodeMap`); override visitor methods for rules that emit or update state.

5. **Implement `VisitTrigDoc()` method** - Two-pass approach: first visit all directives to populate prefix/base tables, then visit all blocks to emit quads.

6. **Implement `VisitDirective()` method** - Handle `prefixID()` (extract prefix label and IRI, add to `_prefixes`), `base()` (extract IRI, set `_baseUri`), `sparqlPrefix()` and `sparqlBase()` variants (same logic as Turtle-style).

7. **Implement `VisitBlock()` method** - Route to appropriate child: `TriplesOrGraphContext`, `WrappedGraphContext`, `Triples2Context`, or GRAPH keyword variant; save/restore `_currentGraph` when entering named graphs.

8. **Implement `VisitTriplesOrGraph()` method** - Extract graph name from `labelOrSubject()` if present, save previous graph, visit wrapped graph or predicate-object list, restore graph.

9. **Implement `VisitWrappedGraph()` method** - Visit `triplesBlock()` within current graph context; return null.

10. **Implement `VisitTriplesBlock()` method** - Iterate children: visit `TriplesContext` items and `Triples2Context` items (blank node property lists, collections); delegate to appropriate visitor.

11. **Implement `VisitTriples()` method** - Extract subject from `subject()`, extract verb and objects from `predicateObjectList()`, emit quads for each subject-verb-object combination.

12. **Implement `VisitPredicateObjectList()` method** - Iterate all verb/objectList pairs (handling `;` separator semantics), extract objects for each verb, emit quads to `_quadStore.Append()`.

13. **Create IRI extraction helpers** - `ExtractIriRef()` (strip `<>`), `ExpandPrefixedName()` (split on `:`, lookup prefix, return expanded), `ResolveRelativeIri()` (check if absolute, else resolve against `_baseUri`), `ExtractIri()` (route to appropriate helper).

14. **Create literal extraction helpers** - `ExtractRdfLiteral()` (plain/language-tagged/typed), `ExtractNumericLiteral()` (INTEGER/DECIMAL/DOUBLE + XSD type annotation), `ExtractBooleanLiteral()` (true/false + XSD type), `UnescapeString()` (handle escapes).

15. **Create blank node helpers** - `ExtractBlankNode()` (labeled: reuse via `_blankNodeMap`; anonymous: generate), `GenerateBlankNode()` (increment counter, return `_:bN`), maintain uniqueness throughout document.

16. **Create syntactic sugar handlers** - `ProcessCollection()` (expand `(obj1 obj2 ...)` to rdf:first/rest chain), `ExtractObject()` (route to IRI/blank node/literal/collection handler).

17. **Implement `AppendQuad()` helper** - Call `_quadStore.Append(subject, predicate, obj, _currentGraph)` for each parsed triple; default graph is `urn:x-default:default-graph`.

18. **Create unit test file** `test/TripleStore.Tests/SinglePassTrigLoaderTests.cs` - Copy all test methods from `TriGLoaderTests`, replace `new TriGLoader(quadStore)` with `new SinglePassTrigLoader(quadStore)`, keep test logic unchanged.

19. **Run tests** - Execute `dotnet test test/TripleStore.Tests --filter SinglePassTrigLoaderTests` to validate new loader against same test cases; fix bugs iteratively without touching existing `TriGLoader`.

20. **Verify no regressions** - Run `dotnet test test/TripleStore.Tests --filter TriGLoaderTests` to confirm original loader still works; both should pass independently.

## Progress Tracking

- [ ] Step 1: Create SinglePassTrigLoader.cs file
- [ ] Step 2: Add ANTLR imports
- [ ] Step 3: Implement LoadFromTextReader() with lexer/parser
- [ ] Step 4: Create TriGVisitorLoader nested class
- [ ] Step 5: Implement VisitTrigDoc()
- [ ] Step 6: Implement VisitDirective()
- [ ] Step 7: Implement VisitBlock()
- [ ] Step 8: Implement VisitTriplesOrGraph()
- [ ] Step 9: Implement VisitWrappedGraph()
- [ ] Step 10: Implement VisitTriplesBlock()
- [ ] Step 11: Implement VisitTriples()
- [ ] Step 12: Implement VisitPredicateObjectList()
- [ ] Step 13: Create IRI extraction helpers
- [ ] Step 14: Create literal extraction helpers
- [ ] Step 15: Create blank node helpers
- [ ] Step 16: Create syntactic sugar handlers
- [ ] Step 17: Implement AppendQuad()
- [ ] Step 18: Create unit test file
- [ ] Step 19: Run and fix tests
- [ ] Step 20: Verify no regressions

## Further Considerations

1. **ANTLR parser error handling** – ANTLR throws `ParseCanceledException` on syntax errors. Decide: wrap in custom exception, let bubble, or add error listener for graceful recovery. Tests may need try/catch adjustments.

2. **String unescaping edge cases** – TriG supports `\uXXXX` (4-digit Unicode) and `\UXXXXXXXX` (8-digit Unicode), plus `\\`, `\"`, `\'`, `\n`, `\r`, `\t`. Verify implementation against spec examples.

3. **Blank node reuse across sections** – When same `_:b1` appears in multiple named graphs, should it be same node or separate? Current plan: global `_blankNodeMap` makes it global. Confirm this matches test expectations.

4. **Collection tail semantics** – Empty collection `()` should resolve to `rdf:nil`. Non-empty `(a b c)` creates chain. Verify `rdf:rest` of last node points to `rdf:nil`, not omitted.

5. **Relative IRI resolution** – If `@base <http://example.org/data/>` and relative IRI `<resource>`, result should be `http://example.org/data/resource`. Watch for path joining edge cases (trailing slashes, empty relative).

6. **Deferred dotNetRDF removal** – Keep old `TriGLoader` available; mark as obsolete in comments if desired. Once `SinglePassTrigLoader` is battle-tested, can plan migration or deletion in separate PR.
