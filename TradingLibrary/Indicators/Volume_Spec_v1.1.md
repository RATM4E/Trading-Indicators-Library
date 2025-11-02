# SharedLibrary/Indicators/Volume — Specification (v1.1)

**Goal:** deterministic, cross‑platform‑identical *volume‑based* indicators and analytics for **cTrader (C#)** and **MT5 (MQL5)**, with explicit contracts, warm‑ups, and fallbacks when true trade volume is unavailable. This spec extends v1.0 by adding **Delta** (per‑bar) and **CVD** (Cumulative Volume Delta).

> Implementation is out of scope here. This is a contracts + rules document aligned with `Core/`, `MovingAverages/`, `PriceAction/`, and `Utilities/DataStructures`.

---

## 0) Common Conventions (unchanged)

- **Indexing:** `0 … N-1` (0 = oldest).  
- **Numeric type:** `double`.  
- **NaN policy:** missing/invalid window ⇒ `NaN`. No implicit NaN‑skipping.  
- **Warm‑up:** the exact window length needed; stated per function.  
- **Volume types:**  
  - `TickVolume` (default for spot FX/CFD)  
  - `RealVolume` (if the platform/symbol supports it)  
  The API exposes a `VolumeKind` parameter; if a requested kind is unavailable, use the **declared fallback** (see VolumeSource below).
- **Batch vs Stateful parity:** identical outputs within ±1e‑12.  
- **Timeframe policy:** all stateful “range/anchored” computations are **minute‑based** under the hood (M1 “bus”), with deterministic aggregation to higher TFs.

---

## 1) Volume Source & Aggregation Abstraction (unchanged)

### 1.1 Types
```text
enum VolumeKind { TickVolume=0, RealVolume=1 }
enum SourceKind { Ticks=0, M1=1, M5=5, M15=15, M30=30 }
enum FallbackPolicy { Strict=0, UseTickVolume=1, UseM1Synthesis=2 }
```

### 1.2 Source Contract
- `VolumeSource` (abstract) exposes **read‑only** per‑bar sequences: `Price(O/H/L/C)`, `Volume(kind)`, and **minute OHLCV**.  
- **Fallbacks**:  
  - If `Ticks` or `RealVolume` are not available → use `UseTickVolume` (count of ticks) or **M1 synthesis** (distribute bar volume along a synthetic path) deterministically.  
- **Determinism**: The same fallback sequence must be reproducible on both platforms given identical inputs.

> Note: FRVP’s “bar‑to‑ticks” distribution is a valid M1 synthesis pattern for building price‑level histograms when ticks are missing.

---

## 2) Classic Volume Indicators (unchanged from v1.0)

- **OBV** — On‑Balance Volume  
- **A/D** — Accumulation/Distribution  
- **CMF** — Chaikin Money Flow  
- **MFI** — Money Flow Index  
- **PVI/NVI** — Positive/Negative Volume Index (optional)

*(see v1.0 for full definitions, parameters, and interfaces)*

---

## 3) VWAP Family (Minute‑bus + ring buffer) (unchanged)

- **Anchored/Session VWAP**, **Rolling VWAP**, **VWAP Bands**  
- Deterministic minute aggregation and variance via volume‑weighted Welford online algorithm

---

## 4) Volume Profile (RVP/FRVP) (unchanged)

- **Fixed‑Range Volume Profile** with `POC/VAH/VAL/VWAP/Modes`  
- **Rolling Window RVP** — per‑minute sparse histograms + circular queue

---

## 5) Rolling‑Window “Cluster Search” (LTF‑derived) (unchanged)

- Percentile‑based thresholding over reference window; HTF aggregation of LTF outliers

---

## 6) **Delta & CVD (NEW)**

### 6.1 Definitions
- **Delta (per‑bar):** `Δ = BuyVolume − SellVolume` for the bar (or for each non‑time bar from `NonTime/` builders).  
- **CVD (Cumulative Volume Delta):** cumulative sum of `Δ` across a session or rolling window.

### 6.2 Trade Direction Classification (priority ladder)
To split each tick/lot into Buy/Sell we use the best available method in this order, ensuring **determinism** across platforms:

1) **Aggressor Side (if available):** if the data feed exposes aggressor flag (buyer‑initiated/seller‑initiated), use it directly.  
2) **Quote Comparison:** if Bid/Ask snapshots are available at tick time:  
   - `tradePrice ≥ ask – ε` → **Buy**;  
   - `tradePrice ≤ bid + ε` → **Sell**;  
   - else → fallback to #3. (ε = half‑tick to guard rounding)  
3) **Up/Down Tick Rule (Lee/Ready proxy):**  
   - `price > prevPrice` → **Buy**, `price < prevPrice` → **Sell`, `==` → **last non‑equal** sign carry.  
4) **M1 Synthesis Fallback:** when only OHLCV is present (no tick stream), distribute bar volume along a deterministic synthetic path and mark rising segments as Buy, falling as Sell (path defined in `VolumeSource`; e.g., bullish bar: O→L→H→C).

> The chosen method **must be fixed per symbol** and recorded in outputs’ metadata for reproducibility.

### 6.3 Parameters
- `VolumeKind kind` — `TickVolume|RealVolume` (feed‑dependent)  
- `AccumulateMode` for CVD:  
  ```text
  enum AccumulateMode { Session=0, DailyUTC=1, RollingN=2, Continuous=3 }
  ```
  - `Session` — reset at session start (or custom anchor)  
  - `DailyUTC` — reset at 00:00 UTC  
  - `RollingN` — rolling window of `N` bars  
  - `Continuous` — no reset (until explicit reset)
- `NonTimeBinding` — compute on **the same bar sequence** as visualized (Time / Renko / Range / RangeXV / P&F / etc.).  
- `ZeroOnInvert` (optional) — zero small delta when `|Δ| < ε` to reduce flicker.

### 6.4 Inputs & Outputs
- **Inputs:** sequence of bars (time‑based or non‑time) with `open, high, low, close, volume` and, if available, tick stream / bid‑ask snapshots.  
- **Outputs:**  
  - `buyVol[]`, `sellVol[]` — per bar  
  - `delta[] = buyVol − sellVol`  
  - `cvd[]` — cumulative per `AccumulateMode`

### 6.5 Warm‑up
- `Delta`: none (bar‑local).  
- `CVD`: 1 bar (needs previous cumulative term, or 0 at reset anchor).

### 6.6 Interfaces

#### C#
```csharp
public enum AccumulateMode { Session=0, DailyUTC=1, RollingN=2, Continuous=3 }

(double[] buy, double[] sell, double[] delta) Delta(
    double[] open, double[] high, double[] low, double[] close, double[] volume,
    VolumeKind kind = VolumeKind.TickVolume,
    SourceKind sKind = SourceKind.Ticks,
    FallbackPolicy fp = FallbackPolicy.UseM1Synthesis);

(double[] cvd, double[] delta) CVD(
    double[] open, double[] high, double[] low, double[] close, double[] volume,
    AccumulateMode mode = AccumulateMode.Session,
    int rollingN = 0,
    VolumeKind kind = VolumeKind.TickVolume,
    SourceKind sKind = SourceKind.Ticks,
    FallbackPolicy fp = FallbackPolicy.UseM1Synthesis);
```

#### MQL5
```mqh
enum AccumulateMode { Session=0, DailyUTC=1, RollingN=2, Continuous=3 };

void Delta(const double &o[], const double &h[], const double &l[], const double &c[], const double &v[],
           const int vKind, const int sKind, const int fp,
           double &buy[], double &sell[], double &delta[]);

void CVD(const double &o[], const double &h[], const double &l[], const double &c[], const double &v[],
         const int mode, const int rollingN,
         const int vKind, const int sKind, const int fp,
         double &cvd[], double &delta[]);
```

### 6.7 Visualization Hints (for reference indicators)
- **Delta Bar:** histogram with positive (buy‑dominant) / negative (sell‑dominant) columns.  
- **CVD:** line; reset marker at session/daily boundary; divergence overlays vs price.  
- **Non‑time charts:** compute Delta/CVD **after** bar builder finalizes a bar to avoid look‑ahead.

### 6.8 Determinism & Tests
- Fix **classification method** per symbol/instrument; include in metadata header.  
- Provide unit tests comparing tick‑based vs M1‑synth deltas on known fixtures.  
- Ensure cross‑platform minute grid alignment (UTC) when using M1 synthesis.

---

## 7) Interfaces Recap (full list excerpt)

### 7.1 Classic Indicators
```csharp
double[] OBV(double[] close, double[] volume);
double[] AD(double[] high, double[] low, double[] close, double[] volume);
double[] CMF(double[] high, double[] low, double[] close, double[] volume, int period=20);
double[] MFI(double[] high, double[] low, double[] close, double[] volume, int period=14);
double[] PVI(double[] close, double[] volume);
double[] NVI(double[] close, double[] volume);
```
```mqh
void OBV(const double &close[], const double &volume[], double &out[]);
void AD(const double &high[], const double &low[], const double &close[], const double &volume[], double &out[]);
void CMF(const double &high[], const double &low[], const double &close[], const double &volume[], const int period, double &out[]);
void MFI(const double &high[], const double &low[], const double &close[], const double &volume[], const int period, double &out[]);
void PVI(const double &close[], const double &volume[], double &out[]);
void NVI(const double &close[], const double &volume[], double &out[]);
```

### 7.2 VWAP / RVP / Cluster Search (unchanged signatures)
*(see v1.0 for details)*

### 7.3 **Delta & CVD (new)** — (see §6.6)

---

**Version:** 1.1  
**Changelog:** added §6 Delta & CVD: definitions, classification ladder, parameters, I/O, warm‑up, interfaces, tests.  
**Status:** Approved for implementation
