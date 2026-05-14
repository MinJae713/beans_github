# MVP16 Card Icon Sprites

Purpose: placeholder-quality card icon sprites for S14P31A303-274.

Mapping policy:
- Use direct serialized `CardData.cardIcon` references for MVP16 cards.
- Do not add Resources, Addressables, or Sprite Atlas loading for this issue.
- Keep `CardSelectionUI` fallback text for non-MVP cards or cards without `cardIcon`.
- These sprites are symbolic placeholders, not final commercial illustrations.

Covered cards:
- projectile_size_up, projectile_speed_up, hit_knockback_up
- reload_time_down, ammo_capacity_up, projectile_heavy
- hit_apply_poison, hit_apply_burn, hit_apply_frost, hit_apply_electric_stack
- reload_overcharge, projectile_reflect, projectile_explosion
- projectile_pierce, projectile_multishot, projectile_gravity_zero
