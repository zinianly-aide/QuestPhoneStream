# Meta Interaction SDK adapter

No Meta package is included in this change. A future Meta-only assembly should register `MetaInteractionBackend` and map:

- Meta Poke and Ray interactors to `IPointerSource` (`Poke` / `Ray`);
- HandGrab and DistanceGrab to `IManipulationSource`;
- Meta two-hand transformer output to `TransformEvent`.

The adapter must be the only location with Meta SDK types or `META_INTERACTION_SDK` compilation symbols. `Interaction/Core` remains SDK-neutral.
