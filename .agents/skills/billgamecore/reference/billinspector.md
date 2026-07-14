# BillInspector — attribute reference (data authoring)

Source: `Assets/BillGameCore/BillInspector/` (own asmdef `BillInspector.Runtime`, auto-referenced → usable from `Assembly-CSharp`). Namespace `BillInspector`. An Odin-style inspector for ThrowingShot's designer data (weapon tables, buff-ring matrices, arena configs).

## How it hooks in
`BillInspectorEditor` replaces Unity's default inspector **only for types that have ≥1 `Bill…` attribute (or a `[BillButton]` method)**; types without any get the stock inspector. So:
- Put attributes on **any** `MonoBehaviour` or `ScriptableObject` — no base class required.
- Inherit **`BillSerializedMonoBehaviour`** / **`BillSerializedScriptableObject`** *only* when you need Unity to serialize `Dictionary`, `HashSet`, `Tuple`, polymorphic refs, etc. (these use `BillSerializer` + `ISerializationCallbackReceiver` under the hood; mark such fields with `[BillSerialize]`).

## Verified signatures (most-used)
```csharp
[BillSlider(float min, float max)]                 // numeric field slider
[BillMinMaxSlider(float minLimit, float maxLimit)] // Vector2: X=min, Y=max (dual handle)
[BillInfoBox(string message, InfoType type = Info)] // box above field/class; .VisibleIf
[BillButton(string label = null, ButtonSize size = Medium)]  // method → button; .Icon, .EnableIf; params → fields
[BillTableList]                                     // List/Array as a table (each serializable field = column)
//   props: ShowPaging, PageSize=20, IsReadOnly, ShowIndexLabels, MinRowCount
[BillListDrawerSettings]                            // props incl. ShowItemCount, DraggableItems
[BillShowIf(string condition)] / [BillShowIf(condition, object compareValue)]  // .Operator (And/Or), AllowMultiple
```
`InfoType`: `None, Info, Warning, Error` · `ButtonSize`: `Small, Medium, Large` · `ConditionOperator`: `And, Or` · `ColorType`: White/Red/Green/Blue/Yellow/Cyan/Magenta/Orange/Purple.

### Condition expressions (BillShowIf/HideIf/EnableIf/DisableIf)
The condition string can be a **field name**, **method name**, an **enum compare** (2nd arg), or an **`@expression`**:
```csharp
[BillShowIf("isActive")]
[BillShowIf("weaponType", WeaponType.Melee)]
[BillShowIf("@level >= 5 && isReady")]
[BillShowIf("CanShowMethod")]
```

## Full catalog (grouped — see each `*.cs` in `Runtime/Attributes/` for exact params)

**Display** (`DisplayAttributes/`): `BillTitle`, `BillLabelText`, `BillHideLabel`, `BillSuffix`, `BillIndent`, `BillPropertyOrder`, `BillGUIColor`, `BillShowInInspector` (show a property/non-serialized member), `BillHideInPlayMode`.

**Groups** (`GroupAttributes/`): `BillBoxGroup`, `BillFoldoutGroup`, `BillTabGroup`, `BillToggleGroup`, `BillHorizontalGroup`, `BillVerticalGroup`. (Group by passing the same group name/path to multiple members.)

**Drawers** (`DrawerAttributes/`): `BillSlider`, `BillMinMaxSlider`, `BillProgressBar`, `BillDropdown`, `BillEnumToggleButtons`, `BillColorPalette`, `BillAssetSelector`, `BillFilePath`, `BillInlineEditor`, `BillPreviewField`, `BillResizableTextArea`, `BillSearchable`, `BillListDrawerSettings`, `BillTableList`, `BillTableColumnWidth`, `BillDictionaryDrawer`.

**Meta / validation** (`MetaAttributes/`): `BillShowIf`, `BillHideIf`, `BillEnableIf`, `BillDisableIf`, `BillReadOnly`, `BillRequired`, `BillInfoBox`, `BillOnValueChanged("Method")`, `BillValidateInput("Method")`, `BillAssetsOnly`, `BillSceneObjectsOnly`.

**Buttons** (`ButtonAttributes/`): `BillButton`, `BillButtonGroup`, `BillShowResultAs`.

**Serialization** (`Runtime/Serialization/`): `BillSerializedMonoBehaviour`, `BillSerializedScriptableObject` (base classes), `[BillSerialize]` (mark a field for `BillSerializer`).

## Example — TOSSZONE weapon/buff data
```csharp
using BillInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "TOSSZONE/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [BillTitle("Weapon")]
    [BillRequired] public string id;

    [BillBoxGroup("Economy")] [BillSlider(0, 30)] public int price;
    [BillBoxGroup("Economy")] [BillSuffix("s")] public float availableFrom;

    [BillBoxGroup("Combat")] [BillSuffix("s")] public float internalCooldown = 0.4f;
    [BillBoxGroup("Combat")] [BillSuffix("m")] public float aoeRadius;
    [BillBoxGroup("Combat")] [BillMinMaxSlider(0f, 5f)] public Vector2 bounceRange;  // X=min,Y=max

    [BillInfoBox("Higher tiers are rarer", InfoType.Info)]
    [BillSlider(1, 5)] public int tier = 1;

    [BillButton("Validate")] void Validate() { /* sanity checks */ }
}

// Buff-ring tier matrix authored as a table:
[CreateAssetMenu(menuName = "TOSSZONE/Buff Ring Table")]
public class BuffRingTable : ScriptableObject
{
    [System.Serializable] public struct TierRow { public int tier; public float diameter, speed; public int multiplier; }
    [BillTableList(ShowIndexLabels = true)] public TierRow[] tiers;
}
```
