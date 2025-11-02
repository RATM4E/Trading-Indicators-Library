# SharedLibrary/NonTime — Specification (v1.0)

## Purpose
Define unified, platform-agnostic generation of **non‑time‑based charts** (price-driven bars) for both **cTrader (C#)** and **MT5 (MQL5)**.  
These charts are critical for algorithmic trading systems that rely on volatility or price movement rather than time intervals.

---

## 1. Core Principles

| Principle | Description |
|------------|--------------|
| **Determinism** | Same tick or M1-synth feed produces identical bars on all platforms. |
| **Data Source** | Prefer tick data; fallback: deterministic synthetic M1 path (O→L→H→C for bearish, O→H→L→C for bullish). |
| **Bar Definition** | Each bar defined by price movement and optionally volume/delta conditions. |
| **State Machine** | Builders implemented as finite-state machines (`OnPrice(p,v,t)`). |
| **Gap Handling** | Modes: `True` (fill all bricks), `Smooth` (connect last to next). |
| **Session Resets** | Optional (None/Daily/Weekly). |

---

## 2. Chart Types

### 2.1 **Renko**
- **Logic:** draw brick when price moves ≥ `BrickSize` in one direction.  
- **Parameters:**
  - `brickSize` — fixed (points, %), or ATR-adaptive (`ATR×k`).
  - `reversalFactor` — multiplier (1.0–2.0).
  - `wicksMode` — `Hide` / `Show` (Wicked Renko).  
  - `gapMode` — `True` / `Smooth`.
- **Variants:**
  - Classic, Wicked (with wicks), Mean/Mid, ATR-Renko, UniRenko-style.  
- **Use cases:** trend‑following, structure detection, trailing stops.

---

### 2.2 **Range Bars**
- **Logic:** close bar when `High−Low ≥ RangeSize` (independent of time).  
- **Parameters:**
  - `rangeSize` — fixed (points/%), or ATR-adaptive.  
  - `trueRangeMode` — include gap or not.  
  - `wicksMode` — `Hide` / `Show`.  
- **Variants:**
  - Basic Range, True Range, Mean/Median Range.
- **Use cases:** breakout detection, volatility normalization, clean pattern extraction.

---

### 2.3 **RangeXV (Range X + Volume)**  
- **Description:** proprietary extension of RangeX concept known from ATAS.  
  Works on *price distance plus internal volume confirmation* (Range X plus Volume).  
  Algorithm: closed-source, but empirical behaviour ≈ combination of **Trend Step + Reversal Step**, with potential internal weighting by tick/real volume.  
- **Parameters (approximation):**
  - `trendSizePoints` — forward movement required to form bar (e.g. 10–20 ticks).  
  - `reversalSizePoints` — counter‑movement to trigger reversal.  
  - `openOffset` — optional offset for first bar alignment.  
  - `volumeConfirm` — minimal cumulative volume before finalizing bar.  
  - `gapMode`, `wicksMode` — as above.  
- **Use cases:** metals, BTC, ETH, futures — instruments with real or quasi‑real volumes.  
  Excellent in combination with **Cumulative Volume Delta (CVD)** or **Delta Bars** for detecting genuine participation behind moves.  
- **Note:** algorithm proprietary (ATAS), specification treats it as "RangeX + Volume" model for experimentation.

---

### 2.4 **Point & Figure (P&F)**
- **Logic:** X/O boxes per `boxSize`; reversal after `reversalBoxes`.  
- **Parameters:** `boxSize`, `reversalBoxes`.  
- **Use:** structural trend and breakout analysis.

### 2.5 **Kagi**
- **Logic:** line changes thickness (yin/yang) upon breaking previous swing levels.  
- **Parameters:** `reversalSize` (points or %).  
- **Use:** trend persistence and reversal visualization.

### 2.6 **Line Break (N‑Line Break)**
- **Logic:** new bar only if price exceeds high/low of last N lines.  
- **Parameters:** `lines=3` (default).  
- **Use:** automatic trend vs correction discrimination.

### 2.7 **Volume / Dollar / Tick / Imbalance Bars**
| Type | Close Condition | Best Use |
|------|-----------------|-----------|
| **Volume Bars** | Sum(volume) ≥ Quota | Crypto, futures |
| **Dollar Bars** | Sum(price×volume) ≥ Quota | Mixed markets |
| **Tick Bars** | N ticks processed | low‑latency backtests |
| **Imbalance Bars (dBars)** | Flow imbalance ≥ threshold | Order‑flow analysis |

---

## 3. Integration with Volume Analytics

### 3.1 Delta & Cumulative Volume Delta (CVD)
- **DeltaBar:** Δ = Volume(buys − sells) per bar.  
- **CVD:** Σ Δ across bars (session or rolling).  
- **Parameters:**  
  - `volumeKind` = Tick / Real.  
  - `accumulateMode` = Session / Rolling / ResetOnNewBar.  
  - `displayMode` = Histogram / Line / Overlay.  
- **Combination:** `RangeXV + CVD` → high‑fidelity view of directional volume aggression.

### 3.2 Synchronization
All volume/delta computations use the **same bar builder feed** to avoid desynchronization.  

---

## 4. Implementation Notes

- Builders keep **state object** with last bar, direction, extremes, cumulative volume.  
- Output: stream of bars `{time, open, high, low, close, volume, delta}`.  
- `time` = last contributing tick or minute boundary (for determinism).  
- For testing equivalence: feed identical tick/M1 data to both implementations → byte‑level identical bar series.

---

## 5. Suggested Defaults (empirical)

| Market | Chart | Size Parameter | Reversal | VolumeConfirm |
|---------|--------|----------------|-----------|----------------|
| XAUUSD | RangeXV | 20 p | 10 p | 500 lots tick‑vol |
| BTCUSD | RangeXV | 25 $ | 10 $ | 1000 tick vol |
| ETHUSD | RangeXV | 15 $ | 8 $ | 700 tick vol |
| EURUSD | Range | 15 p | — | — |
| NAS100 | Renko | 30 p | 2× | — |

---

## 6. API Contracts (conceptual)

### C#
```csharp
BarSeries BuildRenko(IEnumerable<Tick> feed, double brickSize, double reversalFactor, bool wicks);
BarSeries BuildRange(IEnumerable<Tick> feed, double rangeSize, bool trueRange);
BarSeries BuildRangeXV(IEnumerable<Tick> feed, double trendSize, double reversalSize, double minVolume);
BarSeries BuildKagi(IEnumerable<Tick> feed, double reversalSize);
BarSeries BuildPointFigure(IEnumerable<Tick> feed, double boxSize, int reversalBoxes);
BarSeries BuildVolumeBars(IEnumerable<Tick> feed, double volumeQuota);
```

### MQL5
```mqh
int BuildRenko(const MqlTick &ticks[], double brickSize, double reversalFactor, bool wicks, Bar &outBars[]);
int BuildRange(const MqlTick &ticks[], double rangeSize, bool trueRange, Bar &outBars[]);
int BuildRangeXV(const MqlTick &ticks[], double trendSize, double reversalSize, double minVolume, Bar &outBars[]);
int BuildKagi(const MqlTick &ticks[], double reversalSize, Bar &outBars[]);
int BuildPointFigure(const MqlTick &ticks[], double boxSize, int reversalBoxes, Bar &outBars[]);
int BuildVolumeBars(const MqlTick &ticks[], double volumeQuota, Bar &outBars[]);
```

---

**Version:** 1.0  
**Status:** Approved for design stage — open algorithms for Renko/Range, placeholder for Range XV (Range X plus Volume) to be refined after empirical reverse‑engineering.
