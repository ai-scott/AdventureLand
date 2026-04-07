# New Item Checklist: [Item Name]

## JSON Entry
- [ ] Open `files/ItemsLibrary.json`
- [ ] Find next available ID (check last entry)
- [ ] Add entry with: id, name, description, category, strength, cost
- [ ] Verify ID is unique (no duplicates)
- [ ] Category is valid: Weapon, Armor, Consumable, Quest, Key, Material

## Construct 3 IDE (Optional)
- [ ] Create item sprite for inventory display
- [ ] Set up pickup trigger (if world item)

## Unique Quest Items (if applicable)
- [ ] Add config to `scripts/external/unique-items/unique-items-config.ts`
- [ ] Define spawn location and conditions
- [ ] Add quest prerequisites

## Validation
- [ ] Run `npm run validate:items`
- [ ] Test item appears in inventory
- [ ] Test item stats are correct
