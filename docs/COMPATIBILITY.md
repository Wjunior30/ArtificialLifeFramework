# Compatibility and distribution policy

Version 0.3.0 is a pre-1.0 local release candidate. Public source contracts are recorded in PublicApi.txt and checked by tests. Breaking API changes require a minor version bump until 1.0; after 1.0 they require a major bump. Bug fixes use patch versions. Binary consumers must rebuild for 0.3.0: optional parameters and record payloads have grown.

SimulationSave envelopes use a kind, schema and SHA-256 payload checksum. The checksum detects accidental corruption; it is not authentication. Settlement saves write schema 2 and accept schema 1 with legacy resource defaults and built-in policies. New metrics start at zero when loading schema 1 because earlier saves did not collect them. Old upgrade events did not store stage, so their typed Stage is null; new events store the actual stage. Source fixture saves from the pre-change binary test migration. LifeWorld and PortableSimulation use separate schema-1 kinds. Unsupported future versions are rejected.

Regional fingerprints use contract version 3. Do not compare them with version 2. Use the full StateHash for replay checks. Observation through event cursors does not change state. Custom policies, actions and queued commands must supply versioned codecs; missing codecs cause explicit restore failures. Do not reuse a codec ID after changing its payload or behavior. Historical upgrade events migrated from schema 1 expose a null Stage because their original payload did not record it.

The default settlement rules preserve Legacy diplomacy, unlimited storage and the last starvation survivor. Sustainable is an opt-in game preset with negotiated diplomacy, starvation extinction and bounded resources. It is not a claim of historical realism or universally superior balance. Integration changes should use paired seeds and mortality per person-year.

SimulationLife.Core, Protocol and Headless retain their existing compatibility contracts. No engine, file-system storage, JSON serializer or rendering dependency is added to ArtificialLife.Core. Applications own serialization, files, clocks, custom rule code and engine observer lifetimes. Observers and adapter cursors must be reattached after loading.

The repository remains **without a license at the owner's explicit request**. No LICENSE file or permissive license declaration is included in the package. No permission grant should be inferred from source availability. The package and symbol package can be built and inspected locally; public distribution is a separate owner decision.
