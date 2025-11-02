# SharedLibrary/Core — Extended Summary

## Overview
The **Core** layer forms the mathematical and logical foundation for all higher modules within the shared indicator/trading framework. It ensures deterministic, cross‑platform‑identical computations between **cTrader (C#)** and **MetaTrader 5 (MQL5)**. Every function in Core follows strict numerical and NaN‑propagation rules, a unified indexing model (0 → oldest … N‑1 → newest), and consistent seeding/warm‑up policies.

Core consists of three primary namespaces:

1. **MathBase** — atomic math utilities, safe operations, normalization, rounding, and statistical functions.
2. **MovingAverages** — complete collection of causal smoothing algorithms (SMA, EMA, WMA, etc.).
3. **PriceAction** — candle geometry, directional ranges, and low‑level OHLC/volatility primitives.

---

## 1. MathBase
### Purpose
Provides platform‑independent mathematical operations, normalization methods, rounding logic, and statistical primitives.

### Standards & Conventions
- **Numeric type:** `double` (IEEE‑754 64‑bit)
- **NaN Policy:** Any invalid or incomplete data produces `NaN`.
- **Epsilon constant:** `EPS = 1e‑12`
- **Safe arithmetic:** Division and logarithms always guarded by EPS thresholds.
- **Rounding:** Implemented via custom modes (`HalfAwayFromZero`, `HalfToEven`, `Truncate`). No reliance on platform‑specific `NormalizeDouble()`.

### Functional Areas
#### 1. Price shortcuts
- `HL2(h,l)` = (H + L)/2  
- `HLC3(h,l,c)` = (H + L + C)/3  
- `OHLC4(o,h,l,c)` = (O + H + L + C)/4

#### 2. Safe operations
- `SafeDivide(num,den)` → NaN on zero denominator.  
- `SafeDivideOrDefault(num,den,defaultValue)` → fallback value.  
- `SafeSqrt(x)` → NaN if x < 0.  
- `SafeLog(x)` → NaN if x ≤ 0.

#### 3. Comparison & validation
- `AlmostEqual(a,b,eps)` — tolerant equality.  
- `IsFinite(x)` — excludes NaN/±Inf.  
- `Clamp(x,lo,hi)` and `Bound01(x)` — bounded ranges.

#### 4. Normalization & quantization
- `Round(value,digits,mode)`  
- `NormalizeDouble(value,digits,mode)`  
- `Quantize(x,step,mode)` — snap to discrete step.  
- `RoundToTick(price,tickSize,mode)` — price rounding respecting tick size.

#### 5. Statistics
All batch functions require fully valid windows; any NaN within the window yields NaN.

- `Mean(values,period)`  
- `Variance(values,period,sample)`  
- `StdDev(values,period,sample)`  
- `Covariance(x,y,period,sample)`  
- `Correlation(x,y,period)`  

#### 6. Affine transforms & helpers
- `Lerp(a,b,t)` / `Unlerp(a,b,x)` / `MapToRange(x,inLo,inHi,outLo,outHi,clamp)`  
- `Sign(x)` (±1,0) — tolerant via EPS.  
- `PercentChange(curr,prev)` — safe relative diff.  
- `Sum(values,period)` — strict summation without NaN‑skipping.

---

## 2. MovingAverages
### Purpose
Defines every deterministic moving‑average variant used in indicators, filters, and adaptive algorithms. All functions exist in **batch** and **stateful (streaming)** forms.

### Unified rules
- Indexing: 0→oldest, N‑1→latest.  
- `period ≥ 1`.  
- NaN inside required window → NaN output.  
- Warm‑up count fixed per type.  
- Seed mode enumeration → `SmaSeed`, `FirstValueSeed`, `ZeroSeed`, `NaNSeed`.  Default = `SmaSeed`.

### Common formulas
| Type | Definition / Notes | Warm‑up |
|------|--------------------|----------|
| **SMA** | Simple mean over P samples. | P |
| **EMA** | α = 2/(P+1), seed = SMA(P). | P |
| **RMA** | Wilder EMA (α = 1/P). | P |
| **WMA** | Linear weights 1..P. | P |
| **TMA** | SMA(SMA(P)). | 2P−1 |
| **HMA** | WMA(2·WMA(P/2) − WMA(P), √P). | max(P,P/2)+√P−1 |
| **DEMA** | 2·EMA − EMA(EMA). | 2P−1 |
| **TEMA** | 3·EMA − 3·EMA(EMA)+EMA(EMA(EMA)). | 3P−2 |
| **ZLEMA** | Zero‑lag: input adjusted by lag = ⌊(P−1)/2⌋. | P (+lag NaN) |
| **MedianMA** | Median of window. | P |
| **SWMA** | Symmetric/triangular weights. | P |
| **LSMA** | Least‑squares regression line endpoint. | P |
| **KAMA** | Kaufman AMA (adaptive α). | variable |
| **T3** | Tillson T3(P,b=0.7). | 3P−2 |

### Streaming interface structure
```text
class <MA>State
  int Period
  double Value
  int WarmupLeft
  bool IsWarmedUp
  double Update(double x)
  void Reset()
```

### Additional conventions
- EMA family supports direct α input via `FromAlpha`, `FromHalfLife`, `FromTau` for identical exponential behaviors across platforms.
- Period rounding: divisions → ceil, roots → ceil, lags → floor.
- `period=1` → passthrough of source.
- No centred/anti‑causal forms inside Core (offline only).

---

## 3. PriceAction
### Purpose
Implements low‑level candle, volatility, and range primitives. Serves as the foundation for ATR, SuperTrend, and other higher indicators.

### Sections
#### A. Candle geometry
- `Range(H,L)` = H−L  
- `RealBody(O,C)` = |C−O|  
- `UpperWick(O,H,C)` = H−max(O,C)  
- `LowerWick(O,L,C)` = min(O,C)−L  
- `BodyToRange(O,H,L,C)` = |C−O|/(H−L)  
- Boolean helpers: `IsBull`, `IsBear`, `IsDoji(maxBodyRatio=0.1)`, `IsInsideBar`, `IsOutsideBar`, `GapUp`, `GapDown`, `IsNRn`, `IsWRn`.

#### B. Returns & changes
- `Change(price)` = Δprice.  
- `PercentChange(price)` = Δ/prev.  
- `LogReturn(price)` = ln(p/p₋₁).  
- `CumulativeReturn(returns)` = ∏(1+r)−1 or exp(Σlogr)−1.

#### C. True Range & Directional Movement (Wilder)
- `TrueRange[t] = max(H−L, |H−C₋₁|, |L−C₋₁|)`  
- `DM+ = (H−H₋₁ > L₋₁−L && H−H₋₁>0) ? H−H₋₁ : 0`  
- `DM− = (L₋₁−L > H−H₋₁ && L₋₁−L>0) ? L₋₁−L : 0`  
- Stateful variants maintain previous H/L/C.

#### D. OHLC Volatility Estimators
All per‑bar σ² (variance) estimators, later aggregated by MA modules.
- Parkinson σ² = [ln(H/L)]² / (4 ln2)  
- Garman–Klass σ² = 0.5·[ln(H/L)]² − (2ln2−1)·[ln(C/O)]²  
- Rogers–Satchell σ² = ln(H/C)·ln(H/O) + ln(L/C)·ln(L/O)  
- Yang–Zhang Kinetic component σ² (K).  

#### E. Windowed High/Low & Donchian
- `Highest(src,P)` / `Lowest(src,P)` — strict full‑window.  
- `Donchian(H,L,P)` → Upper, Lower, Mid = (U+L)/2.

#### F. Swings & Fractals
- `IsSwingHigh(H,kL,kR)` / `IsSwingLow(L,kL,kR)` — causal; true after kR bars.  
- Optional helper `PivotIndexes(mask)`.

#### G. Heikin‑Ashi transform
- Recursive definition:  
  `HA_Close = (O+H+L+C)/4`  
  `HA_Open = (HA_Open₋₁+HA_Close₋₁)/2`  
  `HA_High = max(H,HA_Open,HA_Close)`  
  `HA_Low  = min(L,HA_Open,HA_Close)`  
- Seed (t=0): `HA_Open₀ = (O₀+C₀)/2`, `HA_Close₀ = (O₀+H₀+L₀+C₀)/4`.
- Batch and stateful variants provided.

### NaN / Warm‑up policy summary
- Incomplete or invalid input → NaN.  
- `TrueRange`, `DM`, `Change` → NaN on first bar.  
- `HeikinAshi` → valid on first bar via seed.

---

## Integration rules across Core
1. **Index direction:** left → right chronological.  
2. **No implicit rounding or skipping of NaN** — strict deterministic output.  
3. **Consistent seeds:** SMA for all EMA/RMA types; first bar rules unified.  
4. **Batch vs Stateful parity:** identical numeric results given identical input sequences.  
5. **Platform parity:** every formula yields same floating‑point results within ±1e‑12 tolerance between C# and MQL5.

---

## Next modules (for context)
- `Volatility/` — builds upon `TrueRange`, ATR, Parkinson, etc.
- `Indicators/` — uses MA + PriceAction primitives for SuperTrend, RSI, Bollinger, etc.
- `Trading/` and `PropTrading/` — depend on Core for all risk and signal math.

---
**Document purpose:** reference baseline for developers implementing or auditing shared Core logic across languages.

