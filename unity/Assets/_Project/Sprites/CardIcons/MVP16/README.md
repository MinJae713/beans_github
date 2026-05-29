# MVP16 Card Illustration Sprites

Purpose: generated internal card illustration candidates for S14P31A303-350.

Mapping policy:
- Use direct serialized `CardData.cardIcon` references for MVP16 cards.
- `CardSelectionUI` consumes those references as the inner illustration layer.
- Card frames are resolved separately from card rarity, so frame styling and card art do not share one sprite.
- Do not add Resources, Addressables, or Sprite Atlas loading for this issue.
- Keep `CardSelectionUI` fallback text for non-MVP cards or cards without `cardIcon`.
- These sprites are 1:1 generated illustration candidates, not final commercial illustrations or gameplay truth.

Covered cards:
- projectile_size_up, projectile_speed_up, hit_knockback_up
- reload_time_down, ammo_capacity_up, projectile_heavy
- hit_apply_poison, hit_apply_burn, hit_apply_frost, hit_apply_electric_stack
- reload_overcharge, projectile_reflect, projectile_explosion
- projectile_pierce, projectile_multishot, projectile_gravity_zero
