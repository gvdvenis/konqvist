# Reset on game-definition drift

We treat the game definition as immutable during a running game and store a hash of the seeded definition alongside gameplay state. If startup sees that hash change, it discards the affected gameplay state and resets to a fresh game so old gameplay state is never applied to a different definition.
