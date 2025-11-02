# SharedLibrary/Indicators/TrendIndicators — Specification (v1.0)

**Goal:** deterministic, cross‑platform‑identical trend baselines and trailing stop indicators for **cTrader (C#)** and **MT5 (MQL5)**, aligned with `Core/` rules (MathBase, MovingAverages, PriceAction, Volatility). This document defines **contracts only** (interfaces, parameters, warm‑up, NaN policy, and stateful API shape).

---

## 0. Common Conventions

- **Indexing:** `0 … N-1`, where `N-1` is newest bar. No `ArraySetAsSeries(true)` inside calculators.
- **Types:** `double` for numeric values; `int` for periods; `bool` for flags; `enum` for discrete modes.
- **NaN policy:** any invalid or incomplete input window → output `NaN`. No silent filling/forwarding.
- **Warm‑up:** each indicator specifies the exact minimum bars before first valid value.
- **Batch vs Stateful:** indicators SHOULD provide both when meaningful. Streaming `.Update(...)` must match batch results for the same sequence.
- **Seed policy:** follows referenced primitives (e.g., ATR uses RMA/EMA with **SMA‑seed**).

### 0.1 Enums (shared)
```text
enum TrendDir { Down = -1, Neutral = 0, Up = 1 }
enum Side { Short = -1, Long = 1 }
enum AtrMode { RMA = 0, EMA = 1 }         // for SuperTrend and ATR-dependent stops
```

### 0.2 Dependencies
- `Core/MathBase`: SafeDivide, Clamp, RoundToTick, NaN constants, etc.
- `Core/MovingAverages`: EMA, RMA, WMA, etc.
- `Core/PriceAction`: TrueRange/DM, Donchian, Highest/Lowest, etc.
- `Volatility/ATR`: ATR(period, AtrMode) batch & stateful.

---

## 1) SuperTrend (ST)

**Purpose:** volatility-following baseline + stop; popular trail filter.  
**Reference formula:** _BasicBand = HL2 ± Mult * ATR(period, mode)_; final line flips only on cross‑over rules.

### 1.1 Parameters
- `periodATR: int` — ATR period (≥1).
- `atrMode: AtrMode` — ATR smoothing (RMA/EMA), default = `RMA` (Wilder).
- `multiplier: double` — typically 1.0–5.0.
- *(optional)* `useCloseCross: bool` — flip only on **close** cross vs intrabar. Default: `true` (recommended for determinism).

### 1.2 Inputs
- `high[], low[], close[]` (same length N).

### 1.3 Outputs
- `upLine[]: double` — active upper trailing line or `NaN` when not active.
- `downLine[]: double` — active lower trailing line or `NaN` when not active.
- `trend[]: TrendDir` — `Up` when price is above active line, `Down` when below; `Neutral` during warm‑up.

### 1.4 Warm‑up
- `Warmup = ATR_warmup + 1` (first band requires ATR plus one decision bar). ATR warm‑up depends on mode (RMA/EMA with SMA seed → P bars).

### 1.5 Stateful API
```text
class SuperTrendState
  int PeriodATR
  AtrMode Mode
  double Multiplier
  // Internal: last active line, last trend dir, ATR state
  TrendDir Dir
  int WarmupLeft
  (double up, double down, TrendDir dir) Update(h,l,c)
  void Reset()
```
**Flip rule (deterministic):**  
- Compute `basicUpper = HL2 + Mult*ATR`, `basicLower = HL2 - Mult*ATR`.
- Evolve final lines with “carry” rule: if `basicUpper < prevUpper` keep `basicUpper`, else use smaller of the two (same symmetrically for lower).
- Flip to `Up` when **close** crosses above `prevUpper`; flip to `Down` when **close** crosses below `prevLower`.

### 1.6 Edge Cases
- Any `NaN` in H/L/C or ATR → `NaN` on lines and `Neutral` trend for that bar.
- `multiplier <= 0` → invalid → lines `NaN`, trend `Neutral`.

---

## 2) Parabolic SAR (PSAR)

**Purpose:** trend‑following stop that accelerates in persistent trends.

### 2.1 Parameters
- `step: double` — acceleration step (default 0.02).
- `maxStep: double` — maximum acceleration (default 0.2).
- *(optional)* `initialSide: TrendDir` — initial side if deducible; else computed from first two bars.

### 2.2 Inputs
- `high[], low[]`

### 2.3 Outputs
- `sar[]: double` — SAR value per bar (`NaN` during warm‑up).

### 2.4 Warm‑up
- Minimum 2 bars to define direction and EP (extreme point). First bar → `NaN`.

### 2.5 Stateful API
```text
class ParabolicSarState
  double Step, MaxStep
  TrendDir Dir
  double AF   // acceleration factor
  double EP   // extreme point (highest high in uptrend or lowest low in downtrend)
  double SAR  // current SAR
  int WarmupLeft
  double Update(h,l)
  void Reset()
```

### 2.6 Edge Cases
- Direction switch resets AF to `step`, EP to most recent extreme.
- Enforce SAR not to penetrate last two bars’ highs/lows (classic rule).

---

## 3) Ichimoku Kinko Hyo (full)

**Purpose:** multi‑line trend system and regime filter.

### 3.1 Parameters (defaults classic)
- `convPeriod = 9` (Tenkan‑sen)
- `basePeriod = 26` (Kijun‑sen)
- `spanBPeriod = 52` (Senkou Span B)
- `displacement = 26` (forward shift for cloud; Chikou lag = displacement)
- *(optional)* mid calc uses `(Highest(H, P) + Lowest(L, P))/2` per period

### 3.2 Inputs
- `open[], high[], low[], close[]`

### 3.3 Outputs
- `tenkan[]`, `kijun[]`, `senkouA[]`, `senkouB[]`, `chikou[]`
  - `senkouA[t]` = `(tenkan[t] + kijun[t])/2` **shifted forward by displacement** (batch output must preserve index with forward `NaN` where future bars unavailable).
  - `senkouB[t]` = `mid(High,Low,spanBPeriod)` shifted forward by displacement.
  - `chikou[t]` = `close[t]` **shifted backward by displacement** (leading `NaN` for initial `displacement` bars).

### 3.4 Warm‑up
- `max(convPeriod, basePeriod, spanBPeriod) + displacement` for forward spans; Tenkan/Kijun valid after their own periods.

### 3.5 Stateful API
For online usage, return **unshifted current values** plus an interface to query displaced values if a renderer needs them. Shifting is usually a visualization concern.
```text
class IchimokuState
  int Conv, Base, SpanB, Displacement
  (double tenkan, double kijun, double senkouA_now, double senkouB_now, double chikou_now) Update(o,h,l,c)
  void Reset()
```

### 3.6 Edge Cases
- Highest/Lowest windows must be fully valid.
- Displacement handling: batch includes explicit `NaN` where shifted values fall outside array bounds.

---

## 4) Donchian Trend & Stop

**Purpose:** breakout‑based trend baseline and opposing trailing stop (Turtle classic).

### 4.1 Parameters
- `period: int` — channel lookback.

### 4.2 Inputs
- `high[], low[]`

### 4.3 Outputs
- `trend[]: TrendDir` — `Up` if price > Upper, `Down` if price < Lower else `Neutral`.
- `stopLong[]: double` — typically `Lower(period)` (for Long positions).
- `stopShort[]: double` — typically `Upper(period)` (for Short positions).

### 4.4 Warm‑up
- `period` (Donchian requires full window).

### 4.5 Stateful API
```text
class DonchianTrendState
  int Period
  (TrendDir dir, double stopLong, double stopShort) Update(h,l,close)
  void Reset()
```

---

## 5) Chande Kroll Stop (CKS)

**Purpose:** ATR‑based long/short stops; robust alterative to ST.

### 5.1 Parameters
- `period: int` — ATR period.
- `atrMult: double` — multiplier for the offsets.
- `lookback: int` — lookback applied to high/low before offsetting.

### 5.2 Inputs
- `high[], low[], close[]`

### 5.3 Outputs
- `(longStop[], shortStop[])`

**Form (informal):**  
- `longStop_t   = Highest(High, lookback)_t  - atrMult * ATR_t`  
- `shortStop_t  = Lowest(Low,  lookback)_t   + atrMult * ATR_t`

### 5.4 Warm‑up
- `max(lookback, ATR_warmup)`

### 5.5 Stateful API
```text
class ChandeKrollState
  int Period, Lookback
  double Mult
  (double longStop, double shortStop) Update(h,l,c)
  void Reset()
```

---

## 6) Gann HiLo Activator (GHL)

**Purpose:** simple trend stop via moving averages of High/Low (or rolling min/max).

### 6.1 Parameters
- `period: int`

### 6.2 Inputs
- `high[], low[]`

### 6.3 Outputs
- `(longLine[], shortLine[])`  
  - `longLine` often defined as `SMA(Low, period)` (or rolling min of Low)  
  - `shortLine` as `SMA(High, period)` (or rolling max of High)

### 6.4 Warm‑up
- `period`

### 6.5 Stateful API
```text
class GannHiLoState
  int Period
  (double longLine, double shortLine) Update(h,l)
  void Reset()
```

---

## 7) McGinley Dynamic (MGD)

**Purpose:** adaptive baseline with lower lag than EMA/SMA.

### 7.1 Parameters
- `period: int`
- `k: double = 0.6` (classic default)

### 7.2 Inputs
- `src[]`

### 7.3 Outputs
- `line[]`

### 7.4 Warm‑up
- `period` (seed via SMA as with EMA family, for determinism)

### 7.5 Stateful API
```text
class McGinleyState
  int Period
  double K
  double Update(src)
  void Reset()
```

---

## 8) T3 Baseline (Tillson)

**Purpose:** smooth low‑lag baseline based on cascaded EMAs.

### 8.1 Parameters
- `period: int`
- `b: double = 0.7`

### 8.2 Inputs
- `src[]`

### 8.3 Outputs
- `line[]`

### 8.4 Warm‑up
- `3*period - 2` (EMA cascade warm‑up)

### 8.5 Stateful API
```text
class T3State
  int Period
  double B
  double Update(src)
  void Reset()
```

---

## 9) Kijun-based Baselines & Stops

**Purpose:** widely used Ichimoku components as standalone trend/stop tools.

### 9.1 Kijun
- **Inputs:** `high[], low[]`
- **Params:** `period = 26`
- **Output:** `kijun[] = (Highest(H,period) + Lowest(L,period))/2`
- **Warm‑up:** `period`

### 9.2 Kijun Trend
- **Inputs:** `close[], kijun[]`
- **Output:** `trend[]: TrendDir` (`Up` if `close > kijun`, `Down` if `<`, `Neutral` otherwise)

### 9.3 Kijun Stop
- **Inputs:** `kijun[]`, `side: Side`, *(optional)* `atrMult` or `offsetTicks`
- **Output:** stop line `double[]`
- **Warm‑up:** dependent on chosen offset calculator

---

## 10) Linear Regression Baseline (LSMA / RegLine)

**Purpose:** baseline derived from linear regression endpoint; slope for trend regime.

### 10.1 Parameters
- `period: int`
- *(optional)* `center = false` (only causal; centered variants live outside `Core`)

### 10.2 Inputs
- `src[]`

### 10.3 Outputs
- `regLine[]: double` — endpoint value of rolling OLS line at each bar
- `slope[]: double` — slope of that line (per bar)
- `trend[]: TrendDir` — sign(slope) with EPS tolerance

### 10.4 Warm‑up
- `period`

### 10.5 Stateful API
```text
class RegressionState
  int Period
  (double line, double slope, TrendDir dir) Update(src)
  void Reset()
```

---

## 11) Validation & Determinism Checklist

- **Equality within tolerance:** Batch vs Stateful outputs identical within `±1e-12` across platforms.
- **Warm‑up signaling:** Return `NaN` until complete; `Neutral` trend during warm‑up.
- **No hidden look‑aheads:** All formulas are causal; Ichimoku’s forward cloud is represented with explicit shifts (`NaN` where out of bounds).
- **ATR parity:** ATR mode (RMA/EMA) and seed strictly follow `Volatility/ATR` spec.
- **Rounding:** No implicit rounding; any tick rounding happens at order‑routing layers, not inside indicators.

---

## 12) C# and MQL5 API Shapes (no code)

### 12.1 Batch (C#)
```csharp
(double[] up, double[] down, TrendDir[] dir) SuperTrend(double[] h, double[] l, double[] c, int periodATR, AtrMode mode, double mult, bool useCloseCross = true);
double[] ParabolicSAR(double[] h, double[] l, double step = 0.02, double maxStep = 0.2);
(double[] tenkan, double[] kijun, double[] senkouA, double[] senkouB, double[] chikou) Ichimoku(double[] o, double[] h, double[] l, double[] c, int conv=9, int bas=26, int spanB=52, int disp=26);
TrendDir[] DonchianTrend(double[] h, double[] l, int period);
(double[] stopLong, double[] stopShort) DonchianStop(double[] h, double[] l, int period);
(double[] longStop, double[] shortStop) ChandeKrollStop(double[] h, double[] l, double[] c, int period, double atrMult, int lookback);
(double[] longLine, double[] shortLine) GannHiLo(double[] h, double[] l, int period);
double[] McGinley(double[] src, int period, double k = 0.6);
double[] T3Baseline(double[] src, int period, double b = 0.7);
double[] Kijun(double[] h, double[] l, int period = 26);
TrendDir[] KijunTrend(double[] c, double[] kijun);
double[] KijunStop(Side side, double[] kijun, double atrMult = 0.0, double offsetTicks = 0.0);
double[] RegLine(double[] src, int period);
double[] RegSlope(double[] src, int period);
TrendDir[] RegTrend(double[] slope, double eps = 0.0);
```

### 12.2 Batch (MQL5)
```mqh
// namespace TrendIndicators
void SuperTrend(const double &h[], const double &l[], const double &c[], const int periodATR, const int mode, const double mult, double &up[], double &down[], int &dir[], const bool useCloseCross=true);
void ParabolicSAR(const double &h[], const double &l[], const double step, const double maxStep, double &sar[]);
void Ichimoku(const double &o[], const double &h[], const double &l[], const double &c[], const int conv, const int bas, const int spanB, const int disp, double &tenkan[], double &kijun[], double &senkouA[], double &senkouB[], double &chikou[]);
void DonchianTrend(const double &h[], const double &l[], const int period, int &trend[]);
void DonchianStop(const double &h[], const double &l[], const int period, double &stopLong[], double &stopShort[]);
void ChandeKrollStop(const double &h[], const double &l[], const double &c[], const int period, const double atrMult, const int lookback, double &longStop[], double &shortStop[]);
void GannHiLo(const double &h[], const double &l[], const int period, double &longLine[], double &shortLine[]);
void McGinley(const double &src[], const int period, const double k, double &line[]);
void T3Baseline(const double &src[], const int period, const double b, double &line[]);
void Kijun(const double &h[], const double &l[], const int period, double &line[]);
void KijunTrend(const double &c[], const double &kijun[], int &trend[]);
void KijunStop(const int side, const double &kijun[], const double atrMult, const double offsetTicks, double &stop[]);
void RegLine(const double &src[], const int period, double &line[]);
void RegSlope(const double &src[], const int period, double &slope[]);
void RegTrend(const double &slope[], const double eps, int &trend[]);
```

### 12.3 Stateful class names
- `SuperTrendState`, `ParabolicSarState`, `IchimokuState`, `DonchianTrendState`, `ChandeKrollState`, `GannHiLoState`, `McGinleyState`, `T3State`, `RegressionState`.

---

**Change Log**  
- v1.0 — Initial contract specification for TrendIndicators.
