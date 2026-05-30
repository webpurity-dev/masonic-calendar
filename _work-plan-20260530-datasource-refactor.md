# Work Plan: Data Source Refactoring - craft_data_source.yaml

**Date**: May 30, 2026  
**Status**: In Progress  
**Scope**: Rename data source sections in `craft_data_source.yaml` for clarity (unit vs order)

---

## Scope

**In Scope (craft_data_source.yaml only):**
- Rename unit-specific sections: officers → unit_officers, past_masters → unit_past_heads, joining_past_masters → unit_joining_past_heads, members → unit_members, honorary_members → unit_honorary_members
- Rename order-specific sections: provincial_officers → order_regional_officers, executive_officers → order_executive_officers, member_stats → order_member_stats, succession_list → order_succession_list
- Create new `order_summary` section consolidating heads/deputies from provincial_officers + executive_officers
- Remove duplicate `heads`/`deputy_heads` from individual sections

**Out of Scope (for later):**
- Other degree data sources (royalarch, mark, ram, etc.)
- Domain model refactoring (happens as needed)

---

## Approach

### Phase 1: YAML Refactoring (craft_data_source.yaml)
1. Rename existing section keys (officers → unit_officers, etc.)
2. Create new `order_summary` section with consolidated heads/deputies
3. Remove duplicate metadata from provincial_officers and executive_officers

### Phase 2: Domain Model Updates
1. Add `OrderSummary` property to `DataSourceMapping` class
2. Create `OrderSummaryConfig` class if needed
3. Update/verify `ProvincialOfficersConfig` to work with new structure

### Phase 3: Renderer Updates
1. Update `ExecutiveOfficersSectionRenderer` to load from `order_summary` instead of config.Heads/DeputyHeads
2. Verify `ProvincialOfficersSectionRenderer` works with renamed section
3. Update factory routing if needed

### Phase 4: Validation
1. Build solution (0 errors)
2. Render craft_executive_officers section
3. Render craft_provincial_officers section (if exists with new name)
4. Full master template render
5. Verify no visual regressions

---

## Files Changed

| File | Change | Priority |
|------|--------|----------|
| craft_data_source.yaml | Rename sections, add order_summary | P0 |
| DocumentLayoutLoader.cs | Add OrderSummary property | P1 |
| ExecutiveOfficersSectionRenderer.cs | Load from order_summary | P1 |
| master_v1.yaml | Update section data_mapping if needed | P2 |

---

## Risks & Mitigation

| Risk | Mitigation |
|------|-----------|
| Section keys not found in YAML deserializer | Test build immediately after YAML edit |
| Renderers can't find new section names | Update all factory routing and section references |
| Template variable names mismatch | Verify variable names in Scriban templates match updated model |
| Git revert needed | User confirmed commit exists; test `git checkout HEAD~1` if issues arise |

---

## Rollback Strategy

```powershell
# If critical issues arise:
git checkout HEAD~1 -- document/data_sources/craft_data_source.yaml
# Then revert code changes in reverse order
```

---

## Success Criteria

✅ Build succeeds (0 errors)  
✅ craft_executive_officers section renders without errors  
✅ craft_provincial_officers (or renamed equivalent) section renders  
✅ Full master template renders all 69 sections  
✅ All email links in executive officers sections functional  
✅ No visual regressions in output

---

## Execution Log

### Step 1: YAML Refactoring
- [ ] Rename unit sections
- [ ] Rename order sections
- [ ] Create order_summary
- [ ] Remove duplicates

### Step 2: Domain Model
- [ ] Add OrderSummary property
- [ ] Build and verify

### Step 3: Renderers
- [ ] Update ExecutiveOfficersSectionRenderer
- [ ] Test individual sections
- [ ] Test full render

### Step 4: Cleanup & Validation
- [ ] Remove old code
- [ ] Final build & render test
- [ ] Document any follow-up tasks
