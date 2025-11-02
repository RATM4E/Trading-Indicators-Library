# SharedLibrary/Indicators/Market — Specification (v1.0)

**Goal:** unified, deterministic computation of *market state and regime* indicators for **cTrader (C#)** and **MT5 (MQL5)**.
Includes all metrics describing trend direction, strength, and “trend quality” (regime discrimination: trend vs chop).

---

## 0) Common Conventions

- **Indexing:** chronological (0 oldest).  
- **Numeric type:** `double`.  
- **NaN policy:** incomplete window → `NaN`.  
- **Warm‑up:** exact length of rolling window (or +internal lags).  
- **Normalization:** trend metrics in `[0,1]`; oscillators around `0`.  
- **Smoothing:** via `AvgMode {SMA, EMA, RMA, WMA}` with SMA seed.  
- **Batch vs Stateful parity:** identical results within ±1e‑12.  

---

## 1) Directional Movement & ADX Group

### 1.1 DMI / ADX
**Definition:**  
- `+DM = H_t − H_{t−1}` if up move > down move else 0  
- `−DM = L_{t−1} − L_t` if down move > up move else 0  
- `TR = max(H−L, |H−C_{t−1}|, |L−C_{t−1}|)`  
- `DI+ = 100 × EMA(+DM/TR, n)`  
- `DI− = 100 × EMA(−DM/TR, n)`  
- `DX = 100 × |DI+ − DI−| / (DI+ + DI−)`  
- `ADX = EMA(DX, n)`

**Parameters:** `period=14`, `avgMode=RMA`  
**Outputs:** `(diPlus[], diMinus[], dx[], adx[])`  
**Warm‑up:** `period`

---

### 1.2 ADXR
**Definition:** mean of current and lagged ADX (`lag=period`).  
**Parameters:** `period=14`, `avgMode=EMA`  
**Outputs:** `adxr[] ∈ [0,1]`  
**Warm‑up:** `2×period`

---

### 1.3 DMI Oscillator / DM Quality
- `DMI Osc = (DI+ − DI−)` (centered at 0)  
- `DM Quality = |DI+ − DI−| / (DI+ + DI−)` (∈ [0,1])  
**Outputs:** `osc[]`, `dmq[]`  
**Warm‑up:** same as DMI

---

## 2) Aroon Family

### 2.1 Aroon Up/Down
**Definition:**  
`AroonUp = (period − barsSinceHighest(H)) / period`  
`AroonDown = (period − barsSinceLowest(L)) / period`  
`AroonOsc = AroonUp − AroonDown`  

**Parameters:** `period=25`  
**Outputs:** `(up[], down[], osc[])`  
**Warm‑up:** `period`

---

## 3) Vortex Indicator

**Definition:**  
`VM+ = Σ|H_t − L_{t−1}|`, `VM− = Σ|L_t − H_{t−1}|`, `TRsum = ΣTR`  
`VI+ = VM+ / TRsum`, `VI− = VM− / TRsum`  

**Parameters:** `period=14`  
**Outputs:** `(viPlus[], viMinus[])`  
**Warm‑up:** `period`

---

## 4) Random Walk Index (RWI)

**Definition:**  
`RWIup = max( (H_t − L_{t−n}) / (ATR(n) × √n) )`  
`RWIdown = max( (H_{t−n} − L_t) / (ATR(n) × √n) )`  
**Parameters:** `period=14`  
**Outputs:** `(rwiUp[], rwiDown[])`  
**Warm‑up:** `period`

---

## 5) Efficiency Ratio (ER, Kaufman)

**Definition:**  
`ER = |C_t − C_{t−period}| / Σ|C_i − C_{i−1}|`  
**Outputs:** `er[] ∈ [0,1]`  
**Warm‑up:** `period`

---

## 6) Trend Intensity Index (TII)

**Definition:**  
`basis = MA(C, period)`  
`TII = Σ(max(C−basis,0)) / Σ(|C−basis|)`  
**Outputs:** `tii[] ∈ [0,1]`  
**Parameters:** `period=20`, `basisAvg=AvgMode.SMA`  
**Warm‑up:** `period`

---

## 7) Linear Regression Metrics

### 7.1 Regression Slope
**Definition:** slope of linear regression over last `n` bars.  
**Outputs:** `slope[]` (can be in price units or normalized).  
**Warm‑up:** `period`

### 7.2 Regression R²
**Definition:** coefficient of determination from regression.  
**Outputs:** `r2[] ∈ [0,1]`  
**Warm‑up:** `period`

---

## 8) Choppiness Index (CHOP)

**Definition:**  
`CHOP = log10( ΣTR(period) / (maxH − minL) ) / log10(period)`  
Normalized to `[0,1]`.  
**Parameters:** `period=14`, `normalize=true`  
**Outputs:** `chop[] ∈ [0,1]`  
**Warm‑up:** `period`

---

## 9) Market Meanness Index (MMI)

**Definition:** based on rank transitions within a window (Ehlers).  
Measures frequency of reversals vs monotonic runs.  
**Parameters:** `period=100`  
**Outputs:** `mmi[] ∈ [0,1]` (1 → choppy)  
**Warm‑up:** `period`

---

## 10) Index of Variability (iVAR)

**Definition:**  
Estimate Hurst exponent H by regressing `log(Var(diff_lag(src)))` vs `log(lag)`;  
`iVAR = 2 − H`.  
- `iVAR ≈ 1` → trend persistent  
- `iVAR ≈ 2` → random  
- `iVAR < 1` → anti‑persistent

**Parameters:**  
- `period=100`  
- `lags=[1,2,4,8]`  
- `normalize=true`  
- `logBase=10`

**Inputs:** `src[]`  
**Outputs:** `ivar[]` (normalized [0.5–2] or scaled to [0–100])  
**Warm‑up:** `max(lags)+period`

---

## 11) RWI, MMI, iVAR Synergy (optional regime layer)
These three together define:
- `TrendScore = (1−CHOP) × ER × (1−MMI)`  
- `ChopScore = CHOP × MMI × (iVAR/2)`  
for downstream ML/filters.

---

## 12) Interfaces (Batch, no code)

### C#
```csharp
(double[] diPlus, double[] diMinus, double[] dx, double[] adx) DMI(double[] h, double[] l, double[] c, int period=14, AvgMode mode=AvgMode.RMA);
double[] ADXR(double[] adx, int period=14, AvgMode mode=AvgMode.EMA);
(double[] up, double[] down, double[] osc) Aroon(double[] h, double[] l, int period=25);
(double[] viPlus, double[] viMinus) Vortex(double[] h, double[] l, double[] c, int period=14);
(double[] rwiUp, double[] rwiDown) RWI(double[] h, double[] l, double[] c, int period=14);
double[] EfficiencyRatio(double[] src, int period=10);
double[] TrendIntensity(double[] c, int period=20, AvgMode basis=AvgMode.SMA);
double[] RegSlope(double[] src, int period=20);
double[] RegR2(double[] src, int period=20);
double[] Choppiness(double[] h, double[] l, double[] c, int period=14, bool normalize=true);
double[] MarketMeanness(double[] src, int period=100);
double[] IVar(double[] src, int period=100, int[] lags=null, bool normalize=true);
```

### MQL5
```mqh
void DMI(const double &h[], const double &l[], const double &c[], const int period, const int mode, double &diPlus[], double &diMinus[], double &dx[], double &adx[]);
void ADXR(const double &adx[], const int period, const int mode, double &out[]);
void Aroon(const double &h[], const double &l[], const int period, double &up[], double &down[], double &osc[]);
void Vortex(const double &h[], const double &l[], const double &c[], const int period, double &viPlus[], double &viMinus[]);
void RWI(const double &h[], const double &l[], const double &c[], const int period, double &rwiUp[], double &rwiDown[]);
void EfficiencyRatio(const double &src[], const int period, double &out[]);
void TrendIntensity(const double &c[], const int period, const int basis, double &out[]);
void RegSlope(const double &src[], const int period, double &out[]);
void RegR2(const double &src[], const int period, double &out[]);
void Choppiness(const double &h[], const double &l[], const double &c[], const int period, const bool normalize, double &out[]);
void MarketMeanness(const double &src[], const int period, double &out[]);
void IVar(const double &src[], const int period, const int &lags[], const bool normalize, double &out[]);
```

---

**Version:** 1.0  
**Status:** Approved for implementation
